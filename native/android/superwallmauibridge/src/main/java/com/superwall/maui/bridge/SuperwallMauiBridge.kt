package com.superwall.maui.bridge

import android.app.Activity
import android.app.Application
import android.os.Handler
import android.os.Looper
import com.superwall.sdk.Superwall
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.delegate.SuperwallDelegateJava
import com.superwall.sdk.delegate.RestorationResult
import com.superwall.sdk.logger.LogLevel
import com.superwall.sdk.misc.ActivityProvider
import com.superwall.sdk.models.entitlements.SubscriptionStatus
import com.superwall.sdk.store.testmode.TestModeBehavior
import com.superwall.sdk.analytics.superwall.SuperwallEventInfo
import com.superwall.sdk.paywall.presentation.PaywallInfo
import com.superwall.sdk.paywall.presentation.PaywallPresentationHandler
import com.superwall.sdk.paywall.presentation.dismiss
import com.superwall.sdk.paywall.presentation.register
import com.superwall.sdk.paywall.presentation.internal.state.PaywallResult
import com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject

interface ActivityProviderCallback {
    fun getCurrentActivity(): Activity?
}

interface ConfigureCallback {
    fun onComplete(success: Boolean, error: String?)
}

interface CompletionCallback {
    fun onComplete()
}

interface RestorePurchasesCallback {
    fun onComplete(resultJson: String)
}

interface PresentationListener {
    fun onPresent(paywallInfoJson: String)
    fun onDismiss(paywallInfoJson: String, resultJson: String)
    fun onError(message: String)
    fun onSkip(reasonJson: String)
    fun onFeature()
}

interface EventListener {
    fun onEvent(eventJson: String)
}

object SuperwallMauiBridge {
    @Volatile
    private var configured = false

    @Volatile
    private var eventListener: EventListener? = null

    @JvmStatic
    fun configure(
        application: Application,
        apiKey: String,
        optionsJson: String?,
        activityProvider: ActivityProviderCallback?,
        callback: ConfigureCallback,
    ) {
        if (configured) {
            configured = true
            callback.onComplete(true, null)
            return
        }

        val options = SuperwallOptions()
        parseLogLevel(optionsJson)?.let { options.logging.level = it }
        parseTestModeBehavior(optionsJson)?.let { options.testModeBehavior = it }
        val sdkActivityProvider = activityProvider?.let { provider ->
            object : ActivityProvider {
                override fun getCurrentActivity(): Activity? = provider.getCurrentActivity()
            }
        }

        Superwall.configure(
            applicationContext = application,
            apiKey = apiKey,
            options = options,
            activityProvider = sdkActivityProvider,
        ) { result ->
            result.fold(
                onSuccess = {
                    configured = true
                    callback.onComplete(true, null)
                },
                onFailure = { error -> callback.onComplete(false, error.message) },
            )
        }

        Superwall.instance.setJavaDelegate(object : SuperwallDelegateJava {
            override fun handleSuperwallEvent(eventInfo: SuperwallEventInfo) {
                val payload = eventJson(eventInfo)
                Handler(Looper.getMainLooper()).post { eventListener?.onEvent(payload) }
            }

            override fun subscriptionStatusDidChange(
                from: SubscriptionStatus,
                to: SubscriptionStatus,
            ) {
                val payload = eventJson(
                    "subscription_status_did_change",
                    mapOf("from" to statusName(from), "to" to statusName(to)),
                )
                Handler(Looper.getMainLooper()).post { eventListener?.onEvent(payload) }
            }
        })
    }

    @JvmStatic
    fun isConfigured(): Boolean = configured

    @JvmStatic
    fun getSubscriptionStatusJson(): String = subscriptionStatusJson()

    @JvmStatic
    fun setEventListener(listener: EventListener?) {
        eventListener = listener
    }

    @JvmStatic
    fun register(
        placement: String,
        paramsJson: String?,
        hasFeature: Boolean,
        listener: PresentationListener?,
    ) {
        if (!configured) {
            listener?.onError("Superwall is not configured")
            return
        }

        val handler = PaywallPresentationHandler()
        handler.onPresent { info -> listener?.onPresent(paywallInfoJson(info)) }
        handler.onDismiss { info, result ->
            listener?.onDismiss(paywallInfoJson(info), paywallResultJson(result))
        }
        handler.onError { error -> listener?.onError(error.message ?: "Unknown Superwall error") }
        handler.onSkip { reason -> listener?.onSkip(skipReasonJson(reason)) }

        try {
            val params = parseParams(paramsJson)
            val feature: (() -> Unit)? = if (hasFeature) ({ listener?.onFeature() }) else null
            Superwall.instance.register(placement, params, handler, feature)
        } catch (error: Exception) {
            listener?.onError(error.message ?: "Unable to register placement")
        }
    }

    @JvmStatic
    fun dismiss(callback: CompletionCallback) {
        if (!configured) {
            callback.onComplete()
            return
        }

        CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate).launch {
            try {
                Superwall.instance.dismiss()
            } finally {
                callback.onComplete()
            }
        }
    }

    @JvmStatic
    fun restorePurchases(callback: RestorePurchasesCallback) {
        if (!configured) {
            callback.onComplete(restoreResultJson(false, "Superwall is not configured"))
            return
        }

        CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate).launch {
            val restoreResult = runCatching { Superwall.instance.restorePurchases() }
                .getOrElse { Result.failure(it) }
            val payload = restoreResult.fold(
                onSuccess = { restoration ->
                    when (restoration) {
                        is RestorationResult.Restored -> restoreResultJson(true, null)
                        is RestorationResult.Failed -> restoreResultJson(false, restoration.error?.message)
                    }
                },
                onFailure = { error -> restoreResultJson(false, error.message) },
            )
            callback.onComplete(payload)
        }
    }

    private fun parseLogLevel(optionsJson: String?): LogLevel? {
        val value = runCatching { JSONObject(optionsJson ?: "{}").optString("logLevel") }.getOrNull()
        return when (value) {
            "debug" -> LogLevel.debug
            "info" -> LogLevel.info
            "warn" -> LogLevel.warn
            "error" -> LogLevel.error
            "none" -> LogLevel.none
            else -> null
        }
    }

    private fun parseTestModeBehavior(optionsJson: String?): TestModeBehavior? {
        val value = runCatching { JSONObject(optionsJson ?: "{}").optString("testModeBehavior") }.getOrNull()
        return when (value) {
            "automatic" -> TestModeBehavior.AUTOMATIC
            "whenEnabledForUser" -> TestModeBehavior.WHEN_ENABLED_FOR_USER
            "never" -> TestModeBehavior.NEVER
            "always" -> TestModeBehavior.ALWAYS
            else -> null
        }
    }

    private fun parseParams(paramsJson: String?): Map<String, Any>? {
        if (paramsJson.isNullOrBlank()) return null
        val json = JSONObject(paramsJson)
        val result = mutableMapOf<String, Any>()
        val keys = json.keys()
        while (keys.hasNext()) {
            val key = keys.next()
            val value = json.get(key)
            require(value is String || value is Boolean || value is Number) {
                "Placement parameter '$key' must be a string, boolean or number"
            }
            result[key] = value
        }
        return result
    }

    private fun paywallInfoJson(info: PaywallInfo): String = JSONObject()
        .put("identifier", info.identifier)
        .put("name", info.name)
        .put("url", info.url.toString())
        .put("presentedByPlacementWithName", info.presentedByEventWithName ?: JSONObject.NULL)
        .put("productIds", JSONArray(info.productIds))
        .toString()

    private fun paywallResultJson(result: PaywallResult): String {
        val json = JSONObject()
        when (result) {
            is PaywallResult.Purchased -> json.put("type", "purchased").put("productId", result.productId)
            is PaywallResult.Declined -> json.put("type", "declined")
            is PaywallResult.Restored -> json.put("type", "restored")
            else -> json.put("type", "unknown")
        }
        return json.toString()
    }

    private fun skipReasonJson(reason: PaywallSkippedReason): String {
        val type = when (reason) {
            is PaywallSkippedReason.Holdout -> "holdout"
            is PaywallSkippedReason.NoAudienceMatch -> "noAudienceMatch"
            is PaywallSkippedReason.PlacementNotFound -> "placementNotFound"
            is PaywallSkippedReason.UserIsSubscribed -> "userIsSubscribed"
            else -> "unknown"
        }
        return JSONObject().put("type", type).toString()
    }

    private fun eventJson(info: SuperwallEventInfo): String = eventJson(info.event.rawName, info.params)

    private fun eventJson(name: String, params: Map<String, Any>): String {
        val jsonParams = JSONObject()
        params.forEach { (key, value) -> jsonParams.put(key, value?.toString() ?: JSONObject.NULL) }
        return JSONObject().put("name", name).put("params", jsonParams).toString()
    }

    private fun statusName(status: SubscriptionStatus): String = when (status) {
        is SubscriptionStatus.Unknown -> "unknown"
        is SubscriptionStatus.Inactive -> "inactive"
        is SubscriptionStatus.Active -> "active"
    }

    private fun subscriptionStatusJson(): String {
        if (!configured) {
            return JSONObject().put("status", "unknown").put("activeEntitlementIds", JSONArray()).toString()
        }

        val status = Superwall.instance.subscriptionStatus.value
        // Entitlement.isActive is not reliably set inside Active; membership in Active is what counts.
        val activeIds = when (status) {
            is SubscriptionStatus.Active -> status.entitlements.map { it.id }.sorted()
            else -> emptyList()
        }
        val state = when (status) {
            is SubscriptionStatus.Unknown -> "unknown"
            is SubscriptionStatus.Active -> "active"
            is SubscriptionStatus.Inactive -> "inactive"
        }
        return JSONObject().put("status", state).put("activeEntitlementIds", JSONArray(activeIds)).toString()
    }

    private fun restoreResultJson(restored: Boolean, error: String?): String {
        val json = JSONObject().put("type", if (restored) "restored" else "failed")
        if (!restored) json.put("error", error ?: JSONObject.NULL)
        return json.toString()
    }
}

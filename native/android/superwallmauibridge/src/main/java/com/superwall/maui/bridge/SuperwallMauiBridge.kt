package com.superwall.maui.bridge

import android.app.Activity
import android.app.Application
import android.net.Uri
import android.os.Handler
import android.os.Looper
import com.superwall.sdk.Superwall
import com.superwall.sdk.analytics.superwall.SuperwallEventInfo
import com.superwall.sdk.config.options.EventTrackingBehavior
import com.superwall.sdk.config.options.PaywallOptions
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.delegate.RestorationResult
import com.superwall.sdk.delegate.SuperwallDelegateJava
import com.superwall.sdk.identity.IdentityOptions
import com.superwall.sdk.identity.identify
import com.superwall.sdk.identity.setUserAttributes
import com.superwall.sdk.logger.LogLevel
import com.superwall.sdk.misc.ActivityProvider
import com.superwall.sdk.models.entitlements.SubscriptionStatus
import com.superwall.sdk.models.triggers.Experiment
import com.superwall.sdk.paywall.presentation.PaywallInfo
import com.superwall.sdk.paywall.presentation.PaywallPresentationHandler
import com.superwall.sdk.paywall.presentation.dismiss
import com.superwall.sdk.paywall.presentation.get_presentation_result.getPresentationResult
import com.superwall.sdk.paywall.presentation.internal.state.PaywallResult
import com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason
import com.superwall.sdk.paywall.presentation.register
import com.superwall.sdk.paywall.presentation.result.PresentationResult
import com.superwall.sdk.store.testmode.TestModeBehavior
import java.net.URI
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

interface JsonCallback {
    fun onComplete(json: String)
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

interface DelegateListener {
    fun onDelegateEvent(kind: String, payloadJson: String)
}

/** Java-friendly surface bound by .NET. Only String/boolean/callback interfaces cross this boundary. */
object SuperwallMauiBridge {
    @JvmStatic
    fun configure(
        application: Application,
        apiKey: String,
        optionsJson: String?,
        activityProvider: ActivityProviderCallback?,
        callback: ConfigureCallback,
    ) = BridgeCore.configure(application, apiKey, optionsJson, activityProvider, callback)

    @JvmStatic
    fun isConfigured(): Boolean = BridgeCore.configured

    @JvmStatic
    fun getSubscriptionStatusJson(): String = BridgeCore.subscriptionStatusJson()

    @JvmStatic
    fun setEventListener(listener: EventListener?) {
        BridgeCore.eventListener = listener
    }

    @JvmStatic
    fun setDelegateListener(listener: DelegateListener?) {
        BridgeCore.delegateListener = listener
    }

    @JvmStatic
    fun register(
        placement: String,
        paramsJson: String?,
        hasFeature: Boolean,
        listener: PresentationListener?,
    ) = BridgeCore.register(placement, paramsJson, hasFeature, listener)

    @JvmStatic
    fun dismiss(callback: CompletionCallback) = BridgeCore.dismiss(callback)

    @JvmStatic
    fun restorePurchases(callback: RestorePurchasesCallback) = BridgeCore.restorePurchases(callback)

    @JvmStatic
    fun identify(userId: String, restorePaywallAssignments: Boolean) =
        BridgeCore.identify(userId, restorePaywallAssignments)

    @JvmStatic
    fun reset() = BridgeCore.reset()

    @JvmStatic
    fun getUserId(): String? = BridgeCore.userId()

    @JvmStatic
    fun isLoggedIn(): Boolean = BridgeCore.isLoggedIn()

    @JvmStatic
    fun setUserAttributes(json: String) = BridgeCore.setUserAttributes(json)

    @JvmStatic
    fun getUserAttributesJson(): String = BridgeCore.userAttributesJson()

    @JvmStatic
    fun getPresentationResult(placement: String, paramsJson: String?, callback: JsonCallback) =
        BridgeCore.getPresentationResult(placement, paramsJson, callback)

    @JvmStatic
    fun preloadAllPaywalls() = BridgeCore.preloadAllPaywalls()

    @JvmStatic
    fun preloadPaywalls(placementsJson: String) = BridgeCore.preloadPaywalls(placementsJson)

    @JvmStatic
    fun isPaywallPresented(): Boolean = BridgeCore.isPaywallPresented()

    /** Safe before configure: the SDK stores the link until configuration finishes. */
    @JvmStatic
    fun handleDeepLink(url: String): Boolean = BridgeCore.handleDeepLink(url)

    @JvmStatic
    fun setLogLevel(level: String) = BridgeCore.setLogLevel(level)
}

// .NET class-parse resolves every non-private member of non-anonymous classes, even package-private ones.
// Rule: non-private members here use only Java/bridge types (SDK objects travel as Any); SDK-typed code is
// private or lives in anonymous objects/lambdas.
private object BridgeCore {
    @Volatile
    var configured = false

    @Volatile
    var eventListener: EventListener? = null

    @Volatile
    var delegateListener: DelegateListener? = null

    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    val mainHandler = Handler(Looper.getMainLooper())

    fun configure(
        application: Application,
        apiKey: String,
        optionsJson: String?,
        activityProvider: ActivityProviderCallback?,
        callback: ConfigureCallback,
    ) {
        if (configured) {
            callback.onComplete(true, null)
            return
        }

        val options = parseOptions(optionsJson)
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

        Superwall.instance.setJavaDelegate(createDelegate())
    }

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

    fun dismiss(callback: CompletionCallback) {
        if (!configured) {
            callback.onComplete()
            return
        }

        scope.launch {
            try {
                Superwall.instance.dismiss()
            } finally {
                callback.onComplete()
            }
        }
    }

    fun restorePurchases(callback: RestorePurchasesCallback) {
        if (!configured) {
            callback.onComplete(restoreResultJson(false, "Superwall is not configured"))
            return
        }

        scope.launch {
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

    fun subscriptionStatusJson(): String =
        if (configured) subscriptionStatusJson(Superwall.instance.subscriptionStatus.value)
        else subscriptionStatusJson(SubscriptionStatus.Unknown)

    // User

    fun identify(userId: String, restorePaywallAssignments: Boolean) {
        if (!Superwall.initialized) return
        Superwall.instance.identify(userId, IdentityOptions(restorePaywallAssignments))
    }

    fun reset() {
        if (!Superwall.initialized) return
        Superwall.instance.reset()
    }

    fun userId(): String? = if (Superwall.initialized) Superwall.instance.userId else null

    fun isLoggedIn(): Boolean = Superwall.initialized && Superwall.instance.isLoggedIn

    fun setUserAttributes(json: String) {
        if (!Superwall.initialized) return
        val parsed = JSONObject(json)
        val attributes = mutableMapOf<String, Any?>()
        val keys = parsed.keys()
        while (keys.hasNext()) {
            val key = keys.next()
            val value = parsed.get(key)
            attributes[key] = if (value == JSONObject.NULL) null else value
        }
        Superwall.instance.setUserAttributes(attributes)
    }

    fun userAttributesJson(): String {
        val json = JSONObject()
        if (!Superwall.initialized) return json.toString()
        Superwall.instance.userAttributes.forEach { (key, value) ->
            val wireValue: Any = when (value) {
                is String, is Boolean, is Int, is Long -> value
                is Number -> value.toDouble().takeIf { it.isFinite() } ?: value.toString()
                else -> value.toString()
            }
            json.put(key, wireValue)
        }
        return json.toString()
    }

    // Presentation control

    fun getPresentationResult(placement: String, paramsJson: String?, callback: JsonCallback) {
        if (!Superwall.initialized) {
            callback.onComplete(errorJson("Superwall is not configured"))
            return
        }

        scope.launch {
            val payload = runCatching {
                Superwall.instance.getPresentationResult(placement, parseParams(paramsJson))
            }.getOrElse { Result.failure(it) }.fold(
                onSuccess = { presentationResultJson(it) },
                onFailure = { errorJson(it.message ?: "Unable to get presentation result") },
            )
            callback.onComplete(payload)
        }
    }

    fun preloadAllPaywalls() {
        if (!Superwall.initialized) return
        Superwall.instance.preloadAllPaywalls()
    }

    fun preloadPaywalls(placementsJson: String) {
        if (!Superwall.initialized) return
        val array = JSONArray(placementsJson)
        val placements = (0 until array.length()).map { array.getString(it) }.toSet()
        Superwall.instance.preloadPaywalls(placements)
    }

    fun isPaywallPresented(): Boolean = Superwall.initialized && Superwall.instance.isPaywallPresented

    // Deep links and runtime settings

    fun handleDeepLink(url: String): Boolean =
        Superwall.handleDeepLink(Uri.parse(url)).getOrDefault(false)

    fun setLogLevel(level: String) {
        val logLevel = logLevel(level) ?: return
        if (!Superwall.initialized) return
        Superwall.instance.logLevel = logLevel
    }

    // Delegate

    fun sendEvent(payload: String) {
        mainHandler.post { eventListener?.onEvent(payload) }
    }

    fun sendDelegateEvent(kind: String, payload: String) {
        mainHandler.post { delegateListener?.onDelegateEvent(kind, payload) }
    }

    // Mapping to the JSON contract (docs/architecture.md §4.4)

    private fun parseOptions(optionsJson: String?): SuperwallOptions {
        val options = SuperwallOptions()
        val json = runCatching { JSONObject(optionsJson ?: "{}") }.getOrElse { JSONObject() }
        logLevel(json.optString("logLevel"))?.let { options.logging.level = it }
        testModeBehavior(json.optString("testModeBehavior"))?.let { options.testModeBehavior = it }
        if (json.has("localeIdentifier")) {
            options.localeIdentifier = json.getString("localeIdentifier")
        }
        eventTrackingBehavior(json.optString("eventTrackingBehavior"))?.let { options.eventTrackingBehavior = it }
        json.optJSONObject("paywalls")?.let { applyPaywallOptions(it, options.paywalls) }
        return options
    }

    private fun applyPaywallOptions(json: JSONObject, paywalls: PaywallOptions) {
        if (json.has("isHapticFeedbackEnabled")) {
            paywalls.isHapticFeedbackEnabled = json.getBoolean("isHapticFeedbackEnabled")
        }
        if (json.has("shouldShowPurchaseFailureAlert")) {
            paywalls.shouldShowPurchaseFailureAlert = json.getBoolean("shouldShowPurchaseFailureAlert")
        }
        if (json.has("shouldPreload")) {
            paywalls.shouldPreload = json.getBoolean("shouldPreload")
        }
        if (json.has("automaticallyDismiss")) {
            paywalls.automaticallyDismiss = json.getBoolean("automaticallyDismiss")
        }
    }

    private fun logLevel(value: String?): LogLevel? = when (value) {
        "debug" -> LogLevel.debug
        "info" -> LogLevel.info
        "warn" -> LogLevel.warn
        "error" -> LogLevel.error
        "none" -> LogLevel.none
        else -> null
    }

    private fun testModeBehavior(value: String?): TestModeBehavior? = when (value) {
        "automatic" -> TestModeBehavior.AUTOMATIC
        "whenEnabledForUser" -> TestModeBehavior.WHEN_ENABLED_FOR_USER
        "never" -> TestModeBehavior.NEVER
        "always" -> TestModeBehavior.ALWAYS
        else -> null
    }

    private fun eventTrackingBehavior(value: String?): EventTrackingBehavior? = when (value) {
        "all" -> EventTrackingBehavior.ALL
        "superwallOnly" -> EventTrackingBehavior.SUPERWALL_ONLY
        "none" -> EventTrackingBehavior.NONE
        else -> null
    }

    fun parseParams(paramsJson: String?): Map<String, Any>? {
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

    fun paywallInfoJson(paywallInfo: Any): String {
        val info = paywallInfo as PaywallInfo
        return JSONObject()
            .put("identifier", info.identifier)
            .put("name", info.name)
            .put("url", info.url.toString())
            .put("presentedByPlacementWithName", info.presentedByEventWithName ?: JSONObject.NULL)
            .put("productIds", JSONArray(info.productIds))
            .toString()
    }

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

    fun presentationResultJson(presentationResult: Any): String = when (val result = presentationResult as PresentationResult) {
        is PresentationResult.PlacementNotFound -> JSONObject().put("type", "placementNotFound")
        is PresentationResult.NoAudienceMatch -> JSONObject().put("type", "noAudienceMatch")
        is PresentationResult.Paywall -> JSONObject().put("type", "paywall").put("experiment", experimentJson(result.experiment))
        is PresentationResult.Holdout -> JSONObject().put("type", "holdout").put("experiment", experimentJson(result.experiment))
        is PresentationResult.PaywallNotAvailable -> JSONObject().put("type", "paywallNotAvailable")
    }.toString()

    private fun experimentJson(experiment: Experiment): JSONObject = JSONObject()
        .put("id", experiment.id)
        .put("groupId", experiment.groupId)
        .put("variantId", experiment.variant.id)
        .put("paywallId", experiment.variant.paywallId ?: JSONObject.NULL)

    fun eventJson(eventInfo: Any): String {
        val info = eventInfo as SuperwallEventInfo
        val params = JSONObject()
        info.params.forEach { (key, value) -> params.put(key, value?.toString() ?: JSONObject.NULL) }
        return JSONObject().put("name", info.event.rawName).put("params", params).toString()
    }

    fun subscriptionStatusJson(subscriptionStatus: Any): String {
        val status = subscriptionStatus as SubscriptionStatus
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

    fun restoreResultJson(restored: Boolean, error: String?): String {
        val json = JSONObject().put("type", if (restored) "restored" else "failed")
        if (!restored) json.put("error", error ?: JSONObject.NULL)
        return json.toString()
    }

    fun errorJson(message: String): String =
        JSONObject().put("type", "error").put("error", message).toString()

    fun urlJson(url: String): String = JSONObject().put("url", url).toString()

    private fun createDelegate(): SuperwallDelegateJava = object : SuperwallDelegateJava {
        override fun handleSuperwallEvent(eventInfo: SuperwallEventInfo) {
            sendEvent(eventJson(eventInfo))
        }

        override fun subscriptionStatusDidChange(from: SubscriptionStatus, to: SubscriptionStatus) {
            sendDelegateEvent("subscriptionStatusDidChange", subscriptionStatusJson(to))
        }

        override fun handleCustomPaywallAction(name: String) {
            sendDelegateEvent("customPaywallAction", JSONObject().put("name", name).toString())
        }

        override fun willPresentPaywall(paywallInfo: PaywallInfo) {
            sendDelegateEvent("willPresentPaywall", paywallInfoJson(paywallInfo))
        }

        override fun didPresentPaywall(paywallInfo: PaywallInfo) {
            sendDelegateEvent("didPresentPaywall", paywallInfoJson(paywallInfo))
        }

        override fun willDismissPaywall(paywallInfo: PaywallInfo) {
            sendDelegateEvent("willDismissPaywall", paywallInfoJson(paywallInfo))
        }

        override fun didDismissPaywall(paywallInfo: PaywallInfo) {
            sendDelegateEvent("didDismissPaywall", paywallInfoJson(paywallInfo))
        }

        override fun paywallWillOpenURL(url: URI) {
            sendDelegateEvent("paywallWillOpenUrl", urlJson(url.toString()))
        }

        override fun paywallWillOpenDeepLink(url: Uri) {
            sendDelegateEvent("paywallWillOpenDeepLink", urlJson(url.toString()))
        }
    }
}

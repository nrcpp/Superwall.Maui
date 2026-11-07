using System.Text.Json;
using Foundation;
using Superwall.Maui.Bridge;
using Superwall.Maui.Platforms.iOS.Native;

namespace Superwall.Maui;

internal sealed partial class SuperwallService
{
    // Native code also retains listeners; these references keep the managed peers alive until a terminal callback.
    private readonly HashSet<PresentationListener> _activeListeners = [];
    private EventListener? _eventListener;
    private DelegateListener? _delegateListener;

    private partial void PlatformConfigure(object? application)
    {
        _eventListener = new EventListener(this);
        _delegateListener = new DelegateListener(this);
        var bridge = SWMBridge.Shared;
        bridge.EventListener = _eventListener;
        bridge.DelegateListener = _delegateListener;
        bridge.Configure(
            _options.GetApiKeyForCurrentPlatform()!,
            BridgeJson.SerializeOptions(_options),
            (success, error) =>
            {
                IsConfigured = success;
                if (!success)
                {
                    Console.WriteLine($"[Superwall.Maui] Superwall configuration failed: {error}");
                }
            });
    }

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature)
    {
        var listener = new PresentationListener(this, handler, feature);
        lock (_activeListeners)
        {
            _activeListeners.Add(listener);
        }

        SWMBridge.Shared.Register(placement, paramsJson, feature is not null, listener);
        return Task.CompletedTask;
    }

    private partial Task PlatformDismissAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SWMBridge.Shared.Dismiss(() => tcs.TrySetResult());
        return tcs.Task;
    }

    private partial Task<SubscriptionStatusInfo> PlatformGetSubscriptionStatusAsync() =>
        Task.FromResult(BridgeJson.ParseSubscriptionStatus(SWMBridge.Shared.GetSubscriptionStatusJson()));

    private partial Task<IReadOnlyList<StoreProductInfo>> PlatformGetProductsAsync(string productIdsJson)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<StoreProductInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        SWMBridge.Shared.GetProducts(productIdsJson, json =>
        {
            try
            {
                completion.TrySetResult(BridgeJson.ParseStoreProducts(json));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                completion.TrySetException(ex);
            }
        });
        return completion.Task;
    }

    private partial Task<StorePurchaseResult> PlatformPurchaseAsync(string productId)
    {
        var completion = new TaskCompletionSource<StorePurchaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SWMBridge.Shared.PurchaseProduct(productId, json =>
        {
            try
            {
                completion.TrySetResult(BridgeJson.ParseStorePurchaseResult(json));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                completion.TrySetException(ex);
            }
        });
        return completion.Task;
    }

    private partial Task<StoreConsumeResult> PlatformConsumePurchaseAsync(string purchaseToken) =>
        Task.FromResult(new StoreConsumeResult(StoreConsumeStatus.NotSupported, "Google Play purchase tokens can only be consumed on Android."));

    private partial Task<RestorePurchasesResult> PlatformRestorePurchasesAsync()
    {
        var tcs = new TaskCompletionSource<RestorePurchasesResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SWMBridge.Shared.RestorePurchases(json =>
        {
            try
            {
                tcs.TrySetResult(BridgeJson.ParseRestoreResult(json));
            }
            catch (JsonException ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private partial void PlatformIdentify(string userId, bool restorePaywallAssignments) =>
        SWMBridge.Shared.Identify(userId, restorePaywallAssignments);

    private partial void PlatformReset() => SWMBridge.Shared.Reset();

    private partial string? PlatformGetUserId() => SWMBridge.Shared.UserId;

    private partial bool PlatformIsLoggedIn() => SWMBridge.Shared.IsLoggedIn;

    private partial void PlatformSetUserAttributes(string attributesJson) => SWMBridge.Shared.SetUserAttributes(attributesJson);

    private partial string PlatformGetUserAttributesJson() => SWMBridge.Shared.GetUserAttributesJson();

    private partial Task<PresentationResult> PlatformGetPresentationResultAsync(string placement, string? paramsJson)
    {
        var tcs = new TaskCompletionSource<PresentationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SWMBridge.Shared.GetPresentationResult(placement, paramsJson, json =>
        {
            try
            {
                tcs.TrySetResult(BridgeJson.ParsePresentationResult(json));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private partial void PlatformPreloadAllPaywalls() => SWMBridge.Shared.PreloadAllPaywalls();

    private partial void PlatformPreloadPaywalls(string placementsJson) => SWMBridge.Shared.PreloadPaywalls(placementsJson);

    private partial bool PlatformIsPaywallPresented() => SWMBridge.Shared.IsPaywallPresented;

    private partial bool PlatformHandleDeepLink(string url) => SWMBridge.Shared.HandleDeepLink(url);

    private partial void PlatformSetLogLevel(string level) => SWMBridge.Shared.SetLogLevel(level);

    private void Release(PresentationListener listener)
    {
        lock (_activeListeners)
        {
            _activeListeners.Remove(listener);
        }
    }

    private sealed class PresentationListener(SuperwallService owner, PaywallPresentationHandler? handler, Action? feature)
        : NSObject, ISWMPresentationListener
    {
        [Export("onPresent:")]
        public void OnPresent(string paywallInfoJson) =>
            Deliver(() =>
            {
                var info = BridgeJson.ParsePaywallInfo(paywallInfoJson);
                return () => handler?.OnPresent?.Invoke(info);
            });

        [Export("onDismiss:resultJson:")]
        public void OnDismiss(string paywallInfoJson, string resultJson)
        {
            owner.Release(this);
            Deliver(() =>
            {
                var info = BridgeJson.ParsePaywallInfo(paywallInfoJson);
                var result = BridgeJson.ParsePaywallResult(resultJson);
                return () => handler?.OnDismiss?.Invoke(info, result);
            });
        }

        [Export("onError:")]
        public void OnError(string message)
        {
            owner.Release(this);
            RunOnMainThread(() => handler?.OnError?.Invoke(message));
        }

        [Export("onSkip:")]
        public void OnSkip(string reasonJson)
        {
            owner.Release(this);
            var reason = BridgeJson.ParseSkipReason(reasonJson);
            RunOnMainThread(() => handler?.OnSkip?.Invoke(reason));
        }

        [Export("onFeature")]
        public void OnFeature() => RunOnMainThread(() => feature?.Invoke());

        private void Deliver(Func<Action> parse)
        {
            Action callback;
            try
            {
                callback = parse();
            }
            catch (JsonException ex)
            {
                callback = () => handler?.OnError?.Invoke($"Superwall.Maui bridge contract error: {ex.Message}");
            }

            RunOnMainThread(callback);
        }
    }

    private sealed class EventListener(SuperwallService owner) : NSObject, ISWMEventListener
    {
        [Export("onEvent:")]
        public void OnEvent(string eventJson)
        {
            SuperwallEventInfo info;
            try
            {
                info = BridgeJson.ParseEvent(eventJson);
                owner.RaiseEvent(info);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Superwall.Maui] Dropped malformed event: {ex.Message}");
            }
        }
    }

    private sealed class DelegateListener(SuperwallService owner) : NSObject, ISWMDelegateListener
    {
        [Export("onDelegateEvent:payloadJson:")]
        public void OnDelegateEvent(string kind, string payloadJson) => owner.HandleDelegateEvent(kind, payloadJson);
    }
}

using System.Text.Json;
using Com.Superwall.Maui.Bridge;
using Microsoft.Maui.ApplicationModel;
using Superwall.Maui.Bridge;

namespace Superwall.Maui;

internal sealed partial class SuperwallService
{
    private readonly HashSet<PresentationListener> _activeListeners = [];
    private readonly HashSet<DismissCallback> _activeDismissCallbacks = [];
    private readonly HashSet<RestoreCallback> _activeRestoreCallbacks = [];
    private EventListener? _eventListener;
    private ActivityProvider? _activityProvider;

    private partial void PlatformConfigure(object? application)
    {
        if (application is not Android.App.Application androidApplication)
        {
            throw new InvalidOperationException("Superwall Android configuration requires an Android Application.");
        }

        _eventListener = new EventListener(this);
        _activityProvider = new ActivityProvider();
        SuperwallMauiBridge.SetEventListener(_eventListener);
        SuperwallMauiBridge.Configure(
            androidApplication,
            _options.GetApiKeyForCurrentPlatform()!,
            BridgeJson.SerializeOptions(_options),
            _activityProvider,
            new ConfigureCallback(this));
    }

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature)
    {
        var listener = new PresentationListener(this, handler, feature);
        lock (_activeListeners)
        {
            _activeListeners.Add(listener);
        }

        SuperwallMauiBridge.Register(placement, paramsJson, feature is not null, listener);
        return Task.CompletedTask;
    }

    private partial Task PlatformDismissAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new DismissCallback(this, tcs);
        lock (_activeDismissCallbacks)
        {
            _activeDismissCallbacks.Add(callback);
        }

        SuperwallMauiBridge.Dismiss(callback);
        return tcs.Task;
    }

    private partial Task<SubscriptionStatusInfo> PlatformGetSubscriptionStatusAsync() =>
        Task.FromResult(BridgeJson.ParseSubscriptionStatus(SuperwallMauiBridge.SubscriptionStatusJson));

    private partial Task<RestorePurchasesResult> PlatformRestorePurchasesAsync()
    {
        var tcs = new TaskCompletionSource<RestorePurchasesResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new RestoreCallback(this, tcs);
        lock (_activeRestoreCallbacks)
        {
            _activeRestoreCallbacks.Add(callback);
        }

        SuperwallMauiBridge.RestorePurchases(callback);
        return tcs.Task;
    }

    private void Release(PresentationListener listener)
    {
        lock (_activeListeners)
        {
            _activeListeners.Remove(listener);
        }
    }

    private void Release(DismissCallback callback)
    {
        lock (_activeDismissCallbacks)
        {
            _activeDismissCallbacks.Remove(callback);
        }
    }

    private void Release(RestoreCallback callback)
    {
        lock (_activeRestoreCallbacks)
        {
            _activeRestoreCallbacks.Remove(callback);
        }
    }

    private sealed class ActivityProvider : Java.Lang.Object, IActivityProviderCallback
    {
        public Android.App.Activity? CurrentActivity => Platform.CurrentActivity;
    }

    private sealed class ConfigureCallback(SuperwallService owner) : Java.Lang.Object, IConfigureCallback
    {
        public void OnComplete(bool success, string? error)
        {
            owner.IsConfigured = success;
            if (!success)
            {
                Console.WriteLine($"[Superwall.Maui] Superwall configuration failed: {error}");
            }
        }
    }

    private sealed class DismissCallback(SuperwallService owner, TaskCompletionSource completion)
        : Java.Lang.Object, ICompletionCallback
    {
        public void OnComplete()
        {
            owner.Release(this);
            RunOnMainThread(() => completion.TrySetResult());
        }
    }

    private sealed class RestoreCallback(SuperwallService owner, TaskCompletionSource<RestorePurchasesResult> completion)
        : Java.Lang.Object, IRestorePurchasesCallback
    {
        public void OnComplete(string resultJson)
        {
            owner.Release(this);
            try
            {
                var result = BridgeJson.ParseRestoreResult(resultJson);
                RunOnMainThread(() => completion.TrySetResult(result));
            }
            catch (JsonException ex)
            {
                RunOnMainThread(() => completion.TrySetException(ex));
            }
        }
    }

    private sealed class PresentationListener(SuperwallService owner, PaywallPresentationHandler? handler, Action? feature)
        : Java.Lang.Object, IPresentationListener
    {
        public void OnPresent(string paywallInfoJson) => Deliver(() =>
        {
            var info = BridgeJson.ParsePaywallInfo(paywallInfoJson);
            return () => handler?.OnPresent?.Invoke(info);
        });

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

        public void OnError(string message)
        {
            owner.Release(this);
            RunOnMainThread(() => handler?.OnError?.Invoke(message));
        }

        public void OnSkip(string reasonJson)
        {
            owner.Release(this);
            var reason = BridgeJson.ParseSkipReason(reasonJson);
            RunOnMainThread(() => handler?.OnSkip?.Invoke(reason));
        }

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

    private sealed class EventListener(SuperwallService owner) : Java.Lang.Object, IEventListener
    {
        public void OnEvent(string eventJson)
        {
            SuperwallEventInfo info;
            try
            {
                info = BridgeJson.ParseEvent(eventJson);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Superwall.Maui] Dropped malformed event: {ex.Message}");
                return;
            }

            owner.RaiseEvent(info);
        }
    }
}

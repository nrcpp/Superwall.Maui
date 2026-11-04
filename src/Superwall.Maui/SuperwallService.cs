using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Superwall.Maui.Bridge;

namespace Superwall.Maui;

internal sealed partial class SuperwallService : ISuperwall
{
    private readonly SuperwallOptions _options;
    private bool _configureStarted;

    internal SuperwallService(SuperwallOptions options) => _options = options;

    public bool IsConfigured { get; private set; }

    public event EventHandler<SuperwallEventInfo>? EventReceived;

    public event EventHandler<PaywallInfo>? PaywallWillPresent;

    public event EventHandler<PaywallInfo>? PaywallDidPresent;

    public event EventHandler<PaywallInfo>? PaywallWillDismiss;

    public event EventHandler<PaywallInfo>? PaywallDidDismiss;

    public event EventHandler<string>? CustomPaywallAction;

    public event EventHandler<Uri>? PaywallWillOpenUrl;

    public event EventHandler<Uri>? PaywallWillOpenDeepLink;

    public event EventHandler<SubscriptionStatusInfo>? SubscriptionStatusChanged;

    public string? UserId => PlatformGetUserId();

    public bool IsLoggedIn => PlatformIsLoggedIn();

    public bool IsPaywallPresented => PlatformIsPaywallPresented();

    public Task RegisterPlacementAsync(
        string placement,
        IReadOnlyDictionary<string, object>? parameters = null,
        PaywallPresentationHandler? handler = null,
        Action? feature = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placement);
        var paramsJson = BridgeJson.SerializeParams(parameters);
        return PlatformRegisterAsync(placement, paramsJson, handler, feature);
    }

    public Task DismissAsync() => PlatformDismissAsync();

    public Task<SubscriptionStatusInfo> GetSubscriptionStatusAsync() => PlatformGetSubscriptionStatusAsync();

    public Task<RestorePurchasesResult> RestorePurchasesAsync() => PlatformRestorePurchasesAsync();

    public void Identify(string userId, bool restorePaywallAssignments = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        PlatformIdentify(userId, restorePaywallAssignments);
    }

    public void Reset() => PlatformReset();

    public void SetUserAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        PlatformSetUserAttributes(BridgeJson.SerializeUserAttributes(attributes));
    }

    public IReadOnlyDictionary<string, object?> GetUserAttributes() =>
        BridgeJson.ParseUserAttributes(PlatformGetUserAttributesJson());

    public Task<PresentationResult> GetPresentationResultAsync(string placement, IReadOnlyDictionary<string, object>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placement);
        return PlatformGetPresentationResultAsync(placement, BridgeJson.SerializeParams(parameters));
    }

    public void PreloadAllPaywalls() => PlatformPreloadAllPaywalls();

    public void PreloadPaywalls(IEnumerable<string> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        PlatformPreloadPaywalls(BridgeJson.SerializePlacements(placements));
    }

    public bool HandleDeepLink(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return PlatformHandleDeepLink(url.OriginalString);
    }

    public void SetLogLevel(SuperwallLogLevel level) => PlatformSetLogLevel(BridgeJson.ToWire(level));

    internal void Configure(object? application = null)
    {
        if (_configureStarted)
        {
            return;
        }

        _configureStarted = true;
        PlatformConfigure(application);
    }

    private partial void PlatformConfigure(object? application);

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature);

    private partial Task PlatformDismissAsync();

    private partial Task<SubscriptionStatusInfo> PlatformGetSubscriptionStatusAsync();

    private partial Task<RestorePurchasesResult> PlatformRestorePurchasesAsync();

    private partial void PlatformIdentify(string userId, bool restorePaywallAssignments);

    private partial void PlatformReset();

    private partial string? PlatformGetUserId();

    private partial bool PlatformIsLoggedIn();

    private partial void PlatformSetUserAttributes(string attributesJson);

    private partial string PlatformGetUserAttributesJson();

    private partial Task<PresentationResult> PlatformGetPresentationResultAsync(string placement, string? paramsJson);

    private partial void PlatformPreloadAllPaywalls();

    private partial void PlatformPreloadPaywalls(string placementsJson);

    private partial bool PlatformIsPaywallPresented();

    private partial bool PlatformHandleDeepLink(string url);

    private partial void PlatformSetLogLevel(string level);

    private void RaiseEvent(SuperwallEventInfo info) => RunOnMainThread(() => EventReceived?.Invoke(this, info));

    private void HandleDelegateEvent(string kind, string payloadJson)
    {
        object? payload;
        try
        {
            payload = BridgeJson.ParseDelegatePayload(kind, payloadJson);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[Superwall.Maui] Dropped malformed delegate event '{kind}': {ex.Message}");
            return;
        }

        RunOnMainThread(() =>
        {
            switch (kind, payload)
            {
                case ("willPresentPaywall", PaywallInfo info): PaywallWillPresent?.Invoke(this, info); break;
                case ("didPresentPaywall", PaywallInfo info): PaywallDidPresent?.Invoke(this, info); break;
                case ("willDismissPaywall", PaywallInfo info): PaywallWillDismiss?.Invoke(this, info); break;
                case ("didDismissPaywall", PaywallInfo info): PaywallDidDismiss?.Invoke(this, info); break;
                case ("customPaywallAction", string name): CustomPaywallAction?.Invoke(this, name); break;
                case ("paywallWillOpenUrl", Uri url): PaywallWillOpenUrl?.Invoke(this, url); break;
                case ("paywallWillOpenDeepLink", Uri url): PaywallWillOpenDeepLink?.Invoke(this, url); break;
                case ("subscriptionStatusDidChange", SubscriptionStatusInfo status): SubscriptionStatusChanged?.Invoke(this, status); break;
            }
        });
    }

    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}

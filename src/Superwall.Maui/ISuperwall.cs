namespace Superwall.Maui;

/// <summary>Entry point to the Superwall SDK. Resolve from DI after <c>UseSuperwall</c>.</summary>
public interface ISuperwall
{
    /// <summary>True once the native SDK finished configuration successfully.</summary>
    bool IsConfigured { get; }

    /// <summary>Raw Superwall analytics events (analogue of <c>SuperwallDelegate.handleSuperwallEvent</c>). Raised on the main thread.</summary>
    event EventHandler<SuperwallEventInfo>? EventReceived;

    /// <summary>
    /// Registers a placement. Completes when the call is handed to the native SDK, not when the paywall closes;
    /// outcomes arrive through <paramref name="handler"/> and <paramref name="feature"/> on the main thread.
    /// </summary>
    Task RegisterPlacementAsync(
        string placement,
        IReadOnlyDictionary<string, object>? parameters = null,
        PaywallPresentationHandler? handler = null,
        Action? feature = null);

    /// <summary>Dismisses the currently presented paywall, if any.</summary>
    Task DismissAsync();

    /// <summary>Reads the native SDK's current subscription state and active entitlement identifiers.</summary>
    Task<SubscriptionStatusInfo> GetSubscriptionStatusAsync();

    /// <summary>
    /// Restores purchases. A successful restore does not imply an active entitlement;
    /// call <see cref="GetSubscriptionStatusAsync"/> to read the resulting state.
    /// </summary>
    Task<RestorePurchasesResult> RestorePurchasesAsync();
}

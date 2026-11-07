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

    /// <summary>Fetches localized store product details for the supplied identifiers. Unknown or unavailable products are omitted.</summary>
    Task<IReadOnlyList<StoreProductInfo>> GetProductsAsync(IEnumerable<string> productIds) =>
        Task.FromException<IReadOnlyList<StoreProductInfo>>(new NotSupportedException("This ISuperwall implementation does not support product lookup."));

    /// <summary>Purchases a StoreKit or Google Play product through Superwall, without presenting a paywall.</summary>
    Task<StorePurchaseResult> PurchaseAsync(string productId) =>
        Task.FromException<StorePurchaseResult>(new NotSupportedException("This ISuperwall implementation does not support direct purchases."));

    /// <summary>Consumes a Google Play consumable token after the backend has verified the transaction and granted its benefit.</summary>
    /// <remarks>Returns <see cref="StoreConsumeStatus.NotSupported"/> on iOS.</remarks>
    Task<StoreConsumeResult> ConsumePurchaseAsync(string purchaseToken) =>
        Task.FromResult(new StoreConsumeResult(StoreConsumeStatus.NotSupported));

    /// <summary>
    /// Restores purchases. A successful restore does not imply an active entitlement;
    /// call <see cref="GetSubscriptionStatusAsync"/> to read the resulting state.
    /// </summary>
    Task<RestorePurchasesResult> RestorePurchasesAsync();

    /// <summary>A paywall is about to be presented. Raised on the main thread.</summary>
    event EventHandler<PaywallInfo>? PaywallWillPresent;

    /// <summary>A paywall was presented. Raised on the main thread.</summary>
    event EventHandler<PaywallInfo>? PaywallDidPresent;

    /// <summary>A paywall is about to be dismissed. Raised on the main thread.</summary>
    event EventHandler<PaywallInfo>? PaywallWillDismiss;

    /// <summary>A paywall was dismissed. Raised on the main thread.</summary>
    event EventHandler<PaywallInfo>? PaywallDidDismiss;

    /// <summary>A paywall element with a custom action was tapped; the argument is the action name from the dashboard.</summary>
    event EventHandler<string>? CustomPaywallAction;

    /// <summary>The paywall is about to open an external URL. Raised on the main thread.</summary>
    event EventHandler<Uri>? PaywallWillOpenUrl;

    /// <summary>The paywall is about to open a deep link. Raised on the main thread.</summary>
    event EventHandler<Uri>? PaywallWillOpenDeepLink;

    /// <summary>The subscription status changed; the argument is the new status. Raised on the main thread.</summary>
    event EventHandler<SubscriptionStatusInfo>? SubscriptionStatusChanged;

    /// <summary>Links the current user to your own user ID. Ignored until the SDK has been configured.</summary>
    /// <param name="userId">Your backend's user identifier.</param>
    /// <param name="restorePaywallAssignments">Wait for paywall assignments from the server before presenting paywalls.</param>
    void Identify(string userId, bool restorePaywallAssignments = false);

    /// <summary>Resets the user ID, on-device paywall assignments and stored user data.</summary>
    void Reset();

    /// <summary>Your user ID after <see cref="Identify"/>, otherwise the SDK's anonymous ID; <c>null</c> before configuration.</summary>
    string? UserId { get; }

    /// <summary>True after <see cref="Identify"/> was called.</summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// Merges attributes into the user's attributes for audience filters and paywall text.
    /// Values: string, bool, number; <c>null</c> removes the key. Keys starting with <c>$</c> are dropped by the SDK.
    /// </summary>
    void SetUserAttributes(IReadOnlyDictionary<string, object?> attributes);

    /// <summary>Current user attributes. Values are string, bool, long, double or null.</summary>
    IReadOnlyDictionary<string, object?> GetUserAttributes();

    /// <summary>Evaluates what registering <paramref name="placement"/> would do, without presenting a paywall.</summary>
    /// <exception cref="InvalidOperationException">The SDK is not configured or could not evaluate the placement.</exception>
    Task<PresentationResult> GetPresentationResultAsync(string placement, IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>Preloads all paywalls. Use with <see cref="SuperwallPaywallOptions.ShouldPreload"/> set to false.</summary>
    void PreloadAllPaywalls();

    /// <summary>Preloads paywalls for the given placements.</summary>
    void PreloadPaywalls(IEnumerable<string> placements);

    /// <summary>True while a paywall is on screen.</summary>
    bool IsPaywallPresented { get; }

    /// <summary>
    /// Passes a deep link to Superwall (paywall previews, redemption codes, <c>deepLink_open</c> triggers).
    /// Safe to call before configuration finishes. Returns true if Superwall handles the link.
    /// </summary>
    bool HandleDeepLink(Uri url);

    /// <summary>Changes the native SDK log level at runtime.</summary>
    void SetLogLevel(SuperwallLogLevel level);
}

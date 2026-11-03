namespace Superwall.Maui;

/// <summary>Per-call callbacks for <see cref="ISuperwall.RegisterPlacementAsync"/>. All callbacks run on the main thread.</summary>
public sealed class PaywallPresentationHandler
{
    public Action<PaywallInfo>? OnPresent { get; set; }

    public Action<PaywallInfo, PaywallResult>? OnDismiss { get; set; }

    public Action<string>? OnError { get; set; }

    public Action<PaywallSkippedReason>? OnSkip { get; set; }
}

public sealed record PaywallInfo(
    string Identifier,
    string Name,
    string Url,
    string? PresentedByPlacementWithName,
    IReadOnlyList<string> ProductIds);

/// <summary>Outcome of a dismissed paywall.</summary>
public abstract record PaywallResult
{
    private PaywallResult()
    {
    }

    public sealed record Purchased(string ProductId) : PaywallResult;

    public sealed record Declined : PaywallResult;

    public sealed record Restored : PaywallResult;
}

public enum PaywallSkippedReason
{
    Holdout,
    NoAudienceMatch,
    PlacementNotFound,
    /// <summary>Reported by the Android SDK only.</summary>
    UserIsSubscribed,
    Unknown,
}

/// <summary>A raw Superwall event, e.g. <c>paywall_open</c>.</summary>
public sealed record SuperwallEventInfo(string Name, IReadOnlyDictionary<string, string?> Params);

public enum SuperwallSubscriptionState
{
    Unknown,
    Inactive,
    Active,
}

public sealed record SubscriptionStatusInfo(
    SuperwallSubscriptionState Status,
    IReadOnlyList<string> ActiveEntitlementIds);

public sealed record RestorePurchasesResult(bool WasSuccessful, string? Error);

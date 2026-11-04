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

/// <summary>What registering a placement would do right now, without presenting anything.</summary>
public abstract record PresentationResult
{
    private PresentationResult()
    {
    }

    /// <summary>The placement is not in any campaign on the dashboard.</summary>
    public sealed record PlacementNotFound : PresentationResult;

    /// <summary>No audience matched, so no paywall would be shown.</summary>
    public sealed record NoAudienceMatch : PresentationResult;

    /// <summary>An audience matched and the user would see a paywall.</summary>
    public sealed record Paywall(Experiment Experiment) : PresentationResult;

    /// <summary>An audience matched but the user is in a holdout group.</summary>
    public sealed record Holdout(Experiment Experiment) : PresentationResult;

    /// <summary>The paywall can't be shown now (e.g. no network, nothing to present from, or already presented).</summary>
    public sealed record PaywallNotAvailable : PresentationResult;
}

/// <summary>Campaign experiment the user was assigned to.</summary>
public sealed record Experiment(string Id, string GroupId, string VariantId, string? PaywallId);

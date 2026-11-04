namespace Superwall.Maui;

/// <summary>Configuration passed to <c>UseSuperwall</c>.</summary>
public sealed class SuperwallOptions
{
    /// <summary>Public API key of the iOS app in the Superwall dashboard (<c>pk_...</c>).</summary>
    public string? AppleApiKey { get; set; }

    /// <summary>Public API key of the Android app in the Superwall dashboard (<c>pk_...</c>).</summary>
    public string? AndroidApiKey { get; set; }

    /// <summary>Native SDK log level.</summary>
    public SuperwallLogLevel LogLevel { get; set; } = SuperwallLogLevel.Warn;

    /// <summary>Controls when the native SDK enters Test Mode. Defaults to automatic.</summary>
    public SuperwallTestModeBehavior TestModeBehavior { get; set; } = SuperwallTestModeBehavior.Automatic;

    /// <summary>Locale used to evaluate audience filters, e.g. <c>en_GB</c>. <c>null</c> keeps the device locale.</summary>
    public string? LocaleIdentifier { get; set; }

    /// <summary>Which events are sent to Superwall servers. <c>null</c> keeps the SDK default (all).</summary>
    public SuperwallEventTrackingBehavior? EventTrackingBehavior { get; set; }

    /// <summary>Paywall presentation options.</summary>
    public SuperwallPaywallOptions Paywalls { get; } = new();

    internal string? GetApiKeyForCurrentPlatform()
    {
#if IOS
        return AppleApiKey;
#elif ANDROID
        return AndroidApiKey;
#else
        return null;
#endif
    }
}

public enum SuperwallLogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    None,
}

public enum SuperwallTestModeBehavior
{
    Automatic,
    WhenEnabledForUser,
    Never,
    Always,
}

public enum SuperwallEventTrackingBehavior
{
    /// <summary>All events are tracked.</summary>
    All,
    /// <summary>Only internal Superwall events; user-initiated tracking, trigger fires and attribute updates are suppressed.</summary>
    SuperwallOnly,
    /// <summary>No events are sent to Superwall servers.</summary>
    None,
}

/// <summary>Paywall options shared by both native SDKs. <c>null</c> keeps the SDK default.</summary>
public sealed class SuperwallPaywallOptions
{
    /// <summary>Haptic feedback on purchase, restore, opening a URL or closing. SDK default: true.</summary>
    public bool? IsHapticFeedbackEnabled { get; set; }

    /// <summary>Show an alert when a purchase fails. SDK default: true.</summary>
    public bool? ShouldShowPurchaseFailureAlert { get; set; }

    /// <summary>Preload paywalls after configuration. Set to false to preload manually. SDK default: true.</summary>
    public bool? ShouldPreload { get; set; }

    /// <summary>Dismiss the paywall automatically after a purchase or restore. SDK default: true.</summary>
    public bool? AutomaticallyDismiss { get; set; }
}

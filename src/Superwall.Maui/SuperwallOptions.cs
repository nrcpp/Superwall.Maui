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

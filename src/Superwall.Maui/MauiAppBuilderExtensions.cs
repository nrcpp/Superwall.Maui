using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
#if IOS || ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif

namespace Superwall.Maui;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ISuperwall"/> as a singleton and configures the native SDK at app launch.
    /// Throws <see cref="InvalidOperationException"/> if the API key for the current platform is missing.
    /// </summary>
    public static MauiAppBuilder UseSuperwall(this MauiAppBuilder builder, Action<SuperwallOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SuperwallOptions();
        configure(options);

#if IOS || ANDROID
        if (string.IsNullOrWhiteSpace(options.GetApiKeyForCurrentPlatform()))
        {
#if IOS
            const string property = nameof(SuperwallOptions.AppleApiKey);
#else
            const string property = nameof(SuperwallOptions.AndroidApiKey);
#endif
            throw new InvalidOperationException(
                $"Superwall API key for the current platform is not set. Set SuperwallOptions.{property} in UseSuperwall().");
        }
#endif

        var service = new SuperwallService(options);
        builder.Services.AddSingleton<ISuperwall>(service);

#if IOS
        builder.ConfigureLifecycleEvents(events => events.AddiOS(ios => ios.FinishedLaunching((_, _) =>
        {
            service.Configure();
            return true;
        })));
#elif ANDROID
                builder.ConfigureLifecycleEvents(events => events.AddAndroid(android => android.OnApplicationCreate(application =>
                        service.Configure(application))));
#endif

        return builder;
    }
}

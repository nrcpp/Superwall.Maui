using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Superwall.Maui.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if IOS
		var apiKey = SampleSecrets.AppleApiKey;
#elif ANDROID
		var apiKey = SampleSecrets.AndroidApiKey;
#else
		string? apiKey = null;
#endif

		// Without a key the app still starts and MainPage explains what is missing (scenario S7).
		if (!string.IsNullOrWhiteSpace(apiKey))
		{
			builder.UseSuperwall(o =>
			{
				o.AppleApiKey = SampleSecrets.AppleApiKey;
				o.AndroidApiKey = SampleSecrets.AndroidApiKey;
				o.LogLevel = SuperwallLogLevel.Debug;
#if ANDROID && DEBUG
				o.TestModeBehavior = SuperwallTestModeBehavior.Always;
#endif
#if IOS && DEBUG
				o.TestModeBehavior = SuperwallTestModeBehavior.Never;
#endif
			});

			// Paywall previews from the dashboard and deepLink_open triggers (URL scheme camity-sw).
			builder.ConfigureLifecycleEvents(events =>
			{
#if IOS
				events.AddiOS(ios => ios
					.SceneWillConnect((_, _, options) =>
					{
						// Null when the scene is not opened by a URL, despite the non-nullable binding.
						if (options.UrlContexts is not { } contexts)
						{
							return;
						}

						foreach (var context in contexts)
						{
							HandleDeepLink(context.Url.AbsoluteString);
						}
					})
					.SceneOpenUrl((_, contexts) =>
					{
						var handled = false;
						foreach (var context in contexts)
						{
							handled |= HandleDeepLink(context.Url.AbsoluteString);
						}

						return handled;
					}));
#elif ANDROID
				events.AddAndroid(android => android
					.OnCreate((activity, _) => HandleDeepLink(activity.Intent?.DataString))
					.OnNewIntent((_, intent) => HandleDeepLink(intent?.DataString)));
#endif
			});
		}

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static bool HandleDeepLink(string? url)
	{
		if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return false;
		}

		var handled = IPlatformApplication.Current?.Services.GetService<ISuperwall>()?.HandleDeepLink(uri) ?? false;
		Console.WriteLine($"[SuperwallSample] deep link {uri} handled={handled}");
		return handled;
	}
}

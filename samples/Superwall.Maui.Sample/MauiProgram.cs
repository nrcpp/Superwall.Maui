using Microsoft.Extensions.Logging;

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
		}

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

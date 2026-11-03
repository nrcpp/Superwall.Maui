using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Superwall.Maui.Sample;

public partial class MainPage : ContentPage
{
	private readonly ObservableCollection<string> _log = [];
	private readonly ISuperwall? _superwall;
	private readonly ILogger? _logger;

	public MainPage()
	{
		InitializeComponent();
		LogView.ItemsSource = _log;

		var services = IPlatformApplication.Current?.Services;
		_superwall = services?.GetService<ISuperwall>();
		_logger = services?.GetService<ILogger<MainPage>>();

		if (_superwall is null)
		{
			StatusLabel.Text = "Superwall API key is missing. Create SampleSecrets.local.cs from SampleSecrets.example.cs.";
			ShowButton.IsEnabled = false;
			DismissButton.IsEnabled = false;
			return;
		}

		_superwall.EventReceived += (_, e) => Log($"event: {e.Name}");
		UpdateStatus();
	}

	private async void OnShowClicked(object? sender, EventArgs e)
	{
		if (_superwall is null)
		{
			return;
		}

		UpdateStatus();
		var placement = PlacementEntry.Text?.Trim() ?? string.Empty;
		Log($"register: {placement}");

		var handler = new PaywallPresentationHandler
		{
			OnPresent = info =>
			{
				Log($"onPresent: {info.Identifier} ({info.Name}) products=[{string.Join(", ", info.ProductIds)}]");
				if (AutoDismissCheckBox.IsChecked)
				{
					DismissLater();
				}
			},
			OnDismiss = (info, result) => Log($"onDismiss: {info.Identifier} result={result}"),
			OnError = error => Log($"onError: {error}"),
			OnSkip = reason => Log($"onSkip: {reason}"),
		};

		try
		{
			await _superwall.RegisterPlacementAsync(placement, handler: handler, feature: () => Log("feature unlocked"));
		}
		catch (Exception ex)
		{
			Log($"register failed: {ex.Message}");
		}
	}

	private async void OnDismissClicked(object? sender, EventArgs e) => await DismissAsync();

	private async void DismissLater()
	{
		await Task.Delay(TimeSpan.FromSeconds(5));
		Log("dismissing from code");
		await DismissAsync();
	}

	private async Task DismissAsync()
	{
		if (_superwall is null)
		{
			return;
		}

		try
		{
			await _superwall.DismissAsync();
			Log("dismiss completed");
		}
		catch (Exception ex)
		{
			Log($"dismiss failed: {ex.Message}");
		}
	}

	private void UpdateStatus() =>
		StatusLabel.Text = $"IsConfigured: {_superwall?.IsConfigured}";

	private void Log(string message)
	{
		_logger?.LogInformation("[SuperwallSample] {Message}", message);
		Console.WriteLine($"[SuperwallSample] {message}");
		_log.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
		UpdateStatus();
	}
}

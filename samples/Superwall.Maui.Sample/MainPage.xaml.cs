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

		_superwall.EventReceived += (_, e) =>
		{
			Log($"event: {e.Name}");
			if (e.Transaction is { } transaction)
			{
				Log($"transaction_complete: {transaction.ProductId}; transactionId={transaction.TransactionId ?? "n/a"}; purchaseTokenPresent={transaction.PurchaseToken is not null}");
			}
		};
		_superwall.PaywallWillPresent += (_, info) => Log($"delegate: willPresent {info.Identifier}");
		_superwall.PaywallDidPresent += (_, info) => Log($"delegate: didPresent {info.Identifier}; IsPaywallPresented={_superwall.IsPaywallPresented}");
		_superwall.PaywallWillDismiss += (_, info) => Log($"delegate: willDismiss {info.Identifier}");
		_superwall.PaywallDidDismiss += (_, info) => Log($"delegate: didDismiss {info.Identifier}");
		_superwall.CustomPaywallAction += (_, name) => Log($"delegate: customAction {name}");
		_superwall.PaywallWillOpenUrl += (_, url) => Log($"delegate: willOpenUrl {url}");
		_superwall.PaywallWillOpenDeepLink += (_, url) => Log($"delegate: willOpenDeepLink {url}");
		_superwall.SubscriptionStatusChanged += (_, status) =>
			Log($"delegate: status changed {status.Status}; [{string.Join(", ", status.ActiveEntitlementIds)}]");
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

	private async void OnStatusClicked(object? sender, EventArgs e)
	{
		if (_superwall is null)
		{
			return;
		}

		try
		{
			var status = await _superwall.GetSubscriptionStatusAsync();
			Log($"subscription status: {status.Status}; active entitlements=[{string.Join(", ", status.ActiveEntitlementIds)}]");
		}
		catch (Exception ex)
		{
			Log($"status query failed: {ex.Message}");
		}
	}

	private async void OnRestoreClicked(object? sender, EventArgs e)
	{
		if (_superwall is null)
		{
			return;
		}

		try
		{
			var restore = await _superwall.RestorePurchasesAsync();
			Log($"restore: success={restore.WasSuccessful}; error={restore.Error ?? "none"}");
			var status = await _superwall.GetSubscriptionStatusAsync();
			Log($"subscription after restore: {status.Status}; active entitlements=[{string.Join(", ", status.ActiveEntitlementIds)}]");
		}
		catch (Exception ex)
		{
			Log($"restore failed: {ex.Message}");
		}
	}

	private async void OnGetProductsClicked(object? sender, EventArgs e)
	{
		if (_superwall is null)
		{
			return;
		}

		var productIds = (ProductIdsEntry.Text ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (productIds.Length == 0)
		{
			Log("enter one or more store product IDs");
			return;
		}

		try
		{
			var products = await _superwall.GetProductsAsync(productIds);
			ProductsPicker.ItemsSource = products.ToList();
			ProductsPicker.SelectedIndex = products.Count == 0 ? -1 : 0;
			foreach (var product in products)
			{
				Log($"product: {product.ProductId} {product.LocalizedPrice} ({product.CurrencyCode ?? "currency n/a"})");
			}
		}
		catch (Exception ex)
		{
			Log($"product lookup failed: {ex.Message}");
		}
	}

	private async void OnPurchaseClicked(object? sender, EventArgs e)
	{
		if (_superwall is null || ProductsPicker.SelectedItem is not StoreProductInfo product)
		{
			Log("select a product before purchasing");
			return;
		}

		try
		{
			var result = await _superwall.PurchaseAsync(product.ProductId);
			Log($"purchase {product.ProductId}: {result.Status}; {result.Error ?? "no error"}");
		}
		catch (Exception ex)
		{
			Log($"purchase failed: {ex.Message}");
		}
	}

	private void OnProductSelectionChanged(object? sender, EventArgs e)
	{
		if (ProductsPicker.SelectedItem is StoreProductInfo product)
		{
			ProductDetailsLabel.Text = $"{product.LocalizedPrice} {product.CurrencyCode}; backend verification required before credits are granted.";
		}
	}

	private async void OnPresentationResultClicked(object? sender, EventArgs e)
	{
		if (_superwall is null)
		{
			return;
		}

		var placement = PlacementEntry.Text?.Trim() ?? string.Empty;
		try
		{
			var result = await _superwall.GetPresentationResultAsync(placement);
			Log($"presentation result for {placement}: {result}");
		}
		catch (Exception ex)
		{
			Log($"presentation result failed: {ex.Message}");
		}
	}

	private void OnPreloadClicked(object? sender, EventArgs e) => Run("preload", () =>
	{
		var placement = PlacementEntry.Text?.Trim() ?? string.Empty;
		_superwall!.PreloadPaywalls([placement]);
		Log($"preload requested for {placement}");
	});

	private void OnIdentifyClicked(object? sender, EventArgs e) => Run("identify", () =>
	{
		var userId = UserIdEntry.Text?.Trim() ?? string.Empty;
		_superwall!.Identify(userId);
		Log($"identify: {userId}");
	});

	private void OnResetClicked(object? sender, EventArgs e) => Run("reset", () =>
	{
		_superwall!.Reset();
		Log("reset");
	});

	private void OnSetAttributesClicked(object? sender, EventArgs e) => Run("set attributes", () =>
	{
		_superwall!.SetUserAttributes(new Dictionary<string, object?>
		{
			["sample_plan"] = "tester",
			["sample_launches"] = 3,
			["sample_beta"] = true,
		});
		Log("attributes set");
	});

	private void OnGetAttributesClicked(object? sender, EventArgs e) => Run("get attributes", () =>
	{
		var attributes = _superwall!.GetUserAttributes();
		Log($"attributes: {string.Join(", ", attributes.Select(a => $"{a.Key}={a.Value ?? "null"}"))}");
	});

	private void Run(string action, Action body)
	{
		if (_superwall is null)
		{
			return;
		}

		try
		{
			body();
		}
		catch (Exception ex)
		{
			Log($"{action} failed: {ex.Message}");
		}
	}

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
		StatusLabel.Text = $"IsConfigured: {_superwall?.IsConfigured}; UserId: {_superwall?.UserId}; LoggedIn: {_superwall?.IsLoggedIn}";

	private void Log(string message)
	{
		_logger?.LogInformation("[SuperwallSample] {Message}", message);
		Console.WriteLine($"[SuperwallSample] {message}");
		_log.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
		UpdateStatus();
	}
}

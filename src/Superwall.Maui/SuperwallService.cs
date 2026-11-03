using Microsoft.Maui.ApplicationModel;
using Superwall.Maui.Bridge;

namespace Superwall.Maui;

internal sealed partial class SuperwallService : ISuperwall
{
    private readonly SuperwallOptions _options;
    private bool _configureStarted;

    internal SuperwallService(SuperwallOptions options) => _options = options;

    public bool IsConfigured { get; private set; }

    public event EventHandler<SuperwallEventInfo>? EventReceived;

    public Task RegisterPlacementAsync(
        string placement,
        IReadOnlyDictionary<string, object>? parameters = null,
        PaywallPresentationHandler? handler = null,
        Action? feature = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placement);
        var paramsJson = BridgeJson.SerializeParams(parameters);
        return PlatformRegisterAsync(placement, paramsJson, handler, feature);
    }

    public Task DismissAsync() => PlatformDismissAsync();

    internal void Configure()
    {
        if (_configureStarted)
        {
            return;
        }

        _configureStarted = true;
        PlatformConfigure();
    }

    private partial void PlatformConfigure();

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature);

    private partial Task PlatformDismissAsync();

    private void RaiseEvent(SuperwallEventInfo info) => RunOnMainThread(() => EventReceived?.Invoke(this, info));

    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}

namespace Superwall.Maui;

internal sealed partial class SuperwallService
{
    // Android bridge: implementation plan, Stage 5.
    private partial void PlatformConfigure() => throw new NotImplementedException("Android support is not implemented yet (Stage 5).");

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature) =>
        throw new NotImplementedException("Android support is not implemented yet (Stage 5).");

    private partial Task PlatformDismissAsync() =>
        throw new NotImplementedException("Android support is not implemented yet (Stage 5).");
}

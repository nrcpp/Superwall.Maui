namespace Superwall.Maui;

internal sealed partial class SuperwallService
{
    private partial void PlatformConfigure() => throw new PlatformNotSupportedException();

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial Task PlatformDismissAsync() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");
}

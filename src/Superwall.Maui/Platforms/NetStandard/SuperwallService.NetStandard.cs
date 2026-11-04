namespace Superwall.Maui;

internal sealed partial class SuperwallService
{
    private partial void PlatformConfigure(object? application) => throw new PlatformNotSupportedException();

    private partial Task PlatformRegisterAsync(string placement, string? paramsJson, PaywallPresentationHandler? handler, Action? feature) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial Task PlatformDismissAsync() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial Task<SubscriptionStatusInfo> PlatformGetSubscriptionStatusAsync() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial Task<RestorePurchasesResult> PlatformRestorePurchasesAsync() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial void PlatformIdentify(string userId, bool restorePaywallAssignments) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial void PlatformReset() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial string? PlatformGetUserId() => null;

    private partial bool PlatformIsLoggedIn() => false;

    private partial void PlatformSetUserAttributes(string attributesJson) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial string PlatformGetUserAttributesJson() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial Task<PresentationResult> PlatformGetPresentationResultAsync(string placement, string? paramsJson) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial void PlatformPreloadAllPaywalls() =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial void PlatformPreloadPaywalls(string placementsJson) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial bool PlatformIsPaywallPresented() => false;

    private partial bool PlatformHandleDeepLink(string url) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");

    private partial void PlatformSetLogLevel(string level) =>
        throw new PlatformNotSupportedException("Superwall is supported on iOS and Android only.");
}

# Changelog

## 1.0.0-preview.1

First public preview. Native SDKs: SuperwallKit 4.18.3 (iOS), superwall-android 2.9.0 (Android).

- `UseSuperwall` configuration with per-platform API keys, log level, Test Mode, locale, event tracking behavior and paywall options.
- `RegisterPlacementAsync` with presentation handler (`OnPresent`, `OnDismiss`, `OnError`, `OnSkip`) and feature block; `DismissAsync`.
- `GetSubscriptionStatusAsync`, `RestorePurchasesAsync`, `SubscriptionStatusChanged`.
- Delegate events: `PaywallWillPresent`/`DidPresent`/`WillDismiss`/`DidDismiss`, `CustomPaywallAction`, `PaywallWillOpenUrl`, `PaywallWillOpenDeepLink`; raw analytics via `EventReceived`.
- `Identify`, `Reset`, `UserId`, `IsLoggedIn`, `SetUserAttributes`, `GetUserAttributes`.
- `GetPresentationResultAsync`, `PreloadAllPaywalls`, `PreloadPaywalls`, `IsPaywallPresented`.
- `HandleDeepLink`, `SetLogLevel`.

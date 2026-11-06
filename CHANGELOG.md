# Changelog

## 1.0.0-preview.3

Updates the iOS native dependency to SuperwallKit 4.19.1. Android remains on superwall-android 2.9.0. No public C# API or bridge-contract changes.

- Includes upstream fixes for user attribute handling, identity reset, configuration refreshes, customer info loading, and purchase plan selection.
- Adds upstream Swift Package Manager support for watchOS 8+ and tvOS 15+; paywall presentation remains available on iOS only.

## 1.0.0-preview.2

Updates the iOS native dependency to SuperwallKit 4.19.0. Android remains on superwall-android 2.9.0. No public C# API or bridge-contract changes.

- Includes upstream fixes for entitlement updates and custom product purchase reporting.
- Includes upstream improvements to refreshing customer entitlements.

## 1.0.0-preview.1

First public preview. Native SDKs: SuperwallKit 4.18.3 (iOS), superwall-android 2.9.0 (Android).

- `UseSuperwall` configuration with per-platform API keys, log level, Test Mode, locale, event tracking behavior and paywall options.
- `RegisterPlacementAsync` with presentation handler (`OnPresent`, `OnDismiss`, `OnError`, `OnSkip`) and feature block; `DismissAsync`.
- `GetSubscriptionStatusAsync`, `RestorePurchasesAsync`, `SubscriptionStatusChanged`.
- Delegate events: `PaywallWillPresent`/`DidPresent`/`WillDismiss`/`DidDismiss`, `CustomPaywallAction`, `PaywallWillOpenUrl`, `PaywallWillOpenDeepLink`; raw analytics via `EventReceived`.
- `Identify`, `Reset`, `UserId`, `IsLoggedIn`, `SetUserAttributes`, `GetUserAttributes`.
- `GetPresentationResultAsync`, `PreloadAllPaywalls`, `PreloadPaywalls`, `IsPaywallPresented`.
- `HandleDeepLink`, `SetLogLevel`.

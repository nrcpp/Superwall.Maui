using System;
using Foundation;
using ObjCRuntime;

// Binding of native/ios SuperwallMauiBridge (Swift, see its generated SuperwallMauiBridge-Swift.h).
// Internal: these types are an implementation detail of Superwall.Maui.
namespace Superwall.Maui.Platforms.iOS.Native
{
    [Internal]
    [BaseType(typeof(NSObject), Name = "SWMBridge")]
    [DisableDefaultCtor]
    interface SWMBridge
    {
        [Static]
        [Export("shared")]
        SWMBridge Shared { get; }

        [Export("isConfigured")]
        bool IsConfigured { get; }

        [NullAllowed, Export("eventListener", ArgumentSemantic.Strong)]
        ISWMEventListener EventListener { get; set; }

        [NullAllowed, Export("delegateListener", ArgumentSemantic.Strong)]
        ISWMDelegateListener DelegateListener { get; set; }

        [Export("configureWithApiKey:optionsJson:completion:")]
        void Configure(string apiKey, [NullAllowed] string optionsJson, Action<bool, NSString> completion);

        [Export("registerWithPlacement:paramsJson:hasFeature:listener:")]
        void Register(string placement, [NullAllowed] string paramsJson, bool hasFeature, [NullAllowed] ISWMPresentationListener listener);

        [Export("dismissWithCompletion:")]
        void Dismiss(Action completion);

        [Export("getSubscriptionStatusJson")]
        string GetSubscriptionStatusJson();

        [Export("restorePurchasesWithCompletion:")]
        void RestorePurchases(Action<NSString> completion);

        [Export("getProductsWithProductIdsJson:completion:")]
        void GetProducts(string productIdsJson, Action<NSString> completion);

        [Export("purchaseProductWithId:completion:")]
        void PurchaseProduct(string productId, Action<NSString> completion);

        [Export("identifyWithUserId:restorePaywallAssignments:")]
        void Identify(string userId, bool restorePaywallAssignments);

        [Export("reset")]
        void Reset();

        [NullAllowed, Export("userId")]
        string UserId { get; }

        [Export("isLoggedIn")]
        bool IsLoggedIn { get; }

        [Export("setUserAttributesWithJson:")]
        void SetUserAttributes(string json);

        [Export("getUserAttributesJson")]
        string GetUserAttributesJson();

        [Export("getPresentationResultWithPlacement:paramsJson:completion:")]
        void GetPresentationResult(string placement, [NullAllowed] string paramsJson, Action<NSString> completion);

        [Export("preloadAllPaywalls")]
        void PreloadAllPaywalls();

        [Export("preloadPaywallsWithPlacementsJson:")]
        void PreloadPaywalls(string placementsJson);

        [Export("isPaywallPresented")]
        bool IsPaywallPresented { get; }

        [Export("handleDeepLinkWithUrl:")]
        bool HandleDeepLink(string url);

        [Export("setLogLevel:")]
        void SetLogLevel(string level);
    }

    interface ISWMPresentationListener { }

    [Internal]
    [Protocol(Name = "SWMPresentationListener")]
    [BaseType(typeof(NSObject))]
    interface SWMPresentationListener
    {
        [Abstract]
        [Export("onPresent:")]
        void OnPresent(string paywallInfoJson);

        [Abstract]
        [Export("onDismiss:resultJson:")]
        void OnDismiss(string paywallInfoJson, string resultJson);

        [Abstract]
        [Export("onError:")]
        void OnError(string message);

        [Abstract]
        [Export("onSkip:")]
        void OnSkip(string reasonJson);

        [Abstract]
        [Export("onFeature")]
        void OnFeature();
    }

    interface ISWMEventListener { }

    [Internal]
    [Protocol(Name = "SWMEventListener")]
    [BaseType(typeof(NSObject))]
    interface SWMEventListener
    {
        [Abstract]
        [Export("onEvent:")]
        void OnEvent(string eventJson);
    }

    interface ISWMDelegateListener { }

    [Internal]
    [Protocol(Name = "SWMDelegateListener")]
    [BaseType(typeof(NSObject))]
    interface SWMDelegateListener
    {
        [Abstract]
        [Export("onDelegateEvent:payloadJson:")]
        void OnDelegateEvent(string kind, string payloadJson);
    }
}

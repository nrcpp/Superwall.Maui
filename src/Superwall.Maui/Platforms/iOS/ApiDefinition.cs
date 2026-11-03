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

        [Export("configureWithApiKey:optionsJson:completion:")]
        void Configure(string apiKey, [NullAllowed] string optionsJson, Action<bool, NSString> completion);

        [Export("registerWithPlacement:paramsJson:hasFeature:listener:")]
        void Register(string placement, [NullAllowed] string paramsJson, bool hasFeature, [NullAllowed] ISWMPresentationListener listener);

        [Export("dismissWithCompletion:")]
        void Dismiss(Action completion);
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
}

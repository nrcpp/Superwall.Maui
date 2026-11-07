using System.Text.Json;
using Superwall.Maui.Bridge;

namespace Superwall.Maui.Tests;

public class BridgeJsonTests
{
    [Fact]
    public void PaywallInfo_ParsesContractFields_AndIgnoresUnknown()
    {
        const string json = """
            {"identifier":"pw_1","name":"Main","url":"https://x/y","presentedByPlacementWithName":"onboarding_complete",
             "productIds":["com.camity.app.premium.weekly"],"futureField":42}
            """;

        var info = BridgeJson.ParsePaywallInfo(json);

        Assert.Equal("pw_1", info.Identifier);
        Assert.Equal("Main", info.Name);
        Assert.Equal("https://x/y", info.Url);
        Assert.Equal("onboarding_complete", info.PresentedByPlacementWithName);
        Assert.Equal(["com.camity.app.premium.weekly"], info.ProductIds);
    }

    [Fact]
    public void PaywallInfo_MissingOptionalFields_AreDefaulted()
    {
        var info = BridgeJson.ParsePaywallInfo("""{"identifier":"pw_1"}""");

        Assert.Null(info.PresentedByPlacementWithName);
        Assert.Empty(info.ProductIds);
        Assert.Equal(string.Empty, info.Name);
    }

    [Fact]
    public void PaywallResult_MapsAllTypes()
    {
        Assert.Equal(new PaywallResult.Purchased("p1"), BridgeJson.ParsePaywallResult("""{"type":"purchased","productId":"p1"}"""));
        Assert.IsType<PaywallResult.Declined>(BridgeJson.ParsePaywallResult("""{"type":"declined"}"""));
        Assert.IsType<PaywallResult.Restored>(BridgeJson.ParsePaywallResult("""{"type":"restored"}"""));
        Assert.Throws<JsonException>(() => BridgeJson.ParsePaywallResult("""{"type":"other"}"""));
    }

    [Theory]
    [InlineData("holdout", PaywallSkippedReason.Holdout)]
    [InlineData("noAudienceMatch", PaywallSkippedReason.NoAudienceMatch)]
    [InlineData("placementNotFound", PaywallSkippedReason.PlacementNotFound)]
    [InlineData("userIsSubscribed", PaywallSkippedReason.UserIsSubscribed)]
    [InlineData("unknown", PaywallSkippedReason.Unknown)]
    [InlineData("somethingNew", PaywallSkippedReason.Unknown)]
    public void SkipReason_Maps(string wire, PaywallSkippedReason expected) =>
        Assert.Equal(expected, BridgeJson.ParseSkipReason($$"""{"type":"{{wire}}"}"""));

    [Fact]
    public void Event_ParsesNameAndParams()
    {
        var e = BridgeJson.ParseEvent("""{"name":"paywall_open","params":{"paywall_id":"pw_1","is_free_trial":null}}""");

        Assert.Equal("paywall_open", e.Name);
        Assert.Equal("pw_1", e.Params["paywall_id"]);
        Assert.Null(e.Params["is_free_trial"]);
    }

    [Fact]
    public void Event_ParsesPurchaseTransactionIdentifiers()
    {
        var transaction = BridgeJson.ParsePurchaseTransaction(
            """{"name":"transaction_complete","params":{},"transaction":{"productId":"credits_10","transactionId":"txn_1","originalTransactionId":"orig_1","purchaseToken":"token_1","signature":"sig_1"}}""");

        Assert.Equal(new PurchaseTransactionInfo("credits_10", "txn_1", "orig_1", "token_1", "sig_1"), transaction);
        Assert.Null(BridgeJson.ParsePurchaseTransaction("""{"name":"paywall_open","params":{}}"""));
    }

    [Fact]
    public void StoreProducts_ParseLocalizedPriceAndCurrency()
    {
        var products = BridgeJson.ParseStoreProducts(
            """{"products":[{"productId":"credits_10","price":4.99,"localizedPrice":"$4.99","currencyCode":"USD","currencySymbol":"$"}]}""");

        Assert.Equal(new StoreProductInfo("credits_10", 4.99m, "$4.99", "USD", "$"), Assert.Single(products));
    }

    [Theory]
    [InlineData("purchased", StorePurchaseStatus.Purchased)]
    [InlineData("canceled", StorePurchaseStatus.Canceled)]
    [InlineData("pending", StorePurchaseStatus.Pending)]
    [InlineData("failed", StorePurchaseStatus.Failed)]
    public void StorePurchaseResult_MapsNativeStatuses(string wire, StorePurchaseStatus expected) =>
        Assert.Equal(expected, BridgeJson.ParseStorePurchaseResult($$"""{"status":"{{wire}}"}""").Status);

    [Fact]
    public void StoreConsumeResult_MapsAndroidStatuses()
    {
        Assert.Equal(StoreConsumeStatus.Consumed, BridgeJson.ParseStoreConsumeResult("""{"status":"consumed"}""").Status);
        Assert.Equal(StoreConsumeStatus.NotSupported, BridgeJson.ParseStoreConsumeResult("""{"status":"notSupported"}""").Status);
        Assert.Equal(StoreConsumeStatus.Failed, BridgeJson.ParseStoreConsumeResult("""{"status":"failed","error":"billing unavailable"}""").Status);
    }

    [Fact]
    public void SubscriptionStatus_ParsesActiveEntitlements()
    {
        var status = BridgeJson.ParseSubscriptionStatus("""{"status":"active","activeEntitlementIds":["pro"]}""");

        Assert.Equal(SuperwallSubscriptionState.Active, status.Status);
        Assert.Equal(["pro"], status.ActiveEntitlementIds);
    }

    [Theory]
    [InlineData("unknown", SuperwallSubscriptionState.Unknown)]
    [InlineData("inactive", SuperwallSubscriptionState.Inactive)]
    [InlineData("future-state", SuperwallSubscriptionState.Unknown)]
    public void SubscriptionStatus_MapsStates(string wire, SuperwallSubscriptionState expected)
    {
        var status = BridgeJson.ParseSubscriptionStatus($$"""{"status":"{{wire}}"}""");

        Assert.Equal(expected, status.Status);
        Assert.Empty(status.ActiveEntitlementIds);
    }

    [Fact]
    public void RestoreResult_DoesNotConflateRestoreSuccessWithActiveEntitlement()
    {
        var restored = BridgeJson.ParseRestoreResult("""{"type":"restored"}""");
        var failed = BridgeJson.ParseRestoreResult("""{"type":"failed","error":"store unavailable"}""");

        Assert.True(restored.WasSuccessful);
        Assert.Null(restored.Error);
        Assert.False(failed.WasSuccessful);
        Assert.Equal("store unavailable", failed.Error);
    }

    [Theory]
    [InlineData(SuperwallLogLevel.Debug, "debug")]
    [InlineData(SuperwallLogLevel.Warn, "warn")]
    [InlineData(SuperwallLogLevel.None, "none")]
    public void Options_SerializeLogLevel(SuperwallLogLevel level, string wire) =>
        Assert.Equal($$"""{"logLevel":"{{wire}}","testModeBehavior":"automatic"}""", BridgeJson.SerializeOptions(new SuperwallOptions { LogLevel = level }));

    [Theory]
    [InlineData(SuperwallTestModeBehavior.Automatic, "automatic")]
    [InlineData(SuperwallTestModeBehavior.WhenEnabledForUser, "whenEnabledForUser")]
    [InlineData(SuperwallTestModeBehavior.Never, "never")]
    [InlineData(SuperwallTestModeBehavior.Always, "always")]
    public void Options_SerializeTestModeBehavior(SuperwallTestModeBehavior behavior, string wire) =>
        Assert.Equal($$"""{"logLevel":"warn","testModeBehavior":"{{wire}}"}""", BridgeJson.SerializeOptions(new SuperwallOptions { TestModeBehavior = behavior }));

    [Fact]
    public void Params_SerializeFlatPrimitives()
    {
        var json = BridgeJson.SerializeParams(new Dictionary<string, object> { ["s"] = "v", ["b"] = true, ["n"] = 3 });

        Assert.Equal("""{"s":"v","b":true,"n":3}""", json);
        Assert.Null(BridgeJson.SerializeParams(null));
        Assert.Throws<ArgumentException>(() => BridgeJson.SerializeParams(new Dictionary<string, object> { ["x"] = new object() }));
    }

    [Fact]
    public void Options_SerializeOptionalFields_OnlyWhenSet()
    {
        var options = new SuperwallOptions
        {
            LocaleIdentifier = "en_GB",
            EventTrackingBehavior = SuperwallEventTrackingBehavior.SuperwallOnly,
        };
        options.Paywalls.ShouldPreload = false;
        options.Paywalls.AutomaticallyDismiss = true;

        Assert.Equal(
            """{"logLevel":"warn","testModeBehavior":"automatic","localeIdentifier":"en_GB","eventTrackingBehavior":"superwallOnly","paywalls":{"shouldPreload":false,"automaticallyDismiss":true}}""",
            BridgeJson.SerializeOptions(options));
    }

    [Theory]
    [InlineData("willPresentPaywall")]
    [InlineData("didPresentPaywall")]
    [InlineData("willDismissPaywall")]
    [InlineData("didDismissPaywall")]
    public void Delegate_PaywallKinds_ParsePaywallInfo(string kind)
    {
        var payload = BridgeJson.ParseDelegatePayload(kind, """{"identifier":"pw_1","name":"Main","url":"https://x","productIds":[]}""");

        Assert.Equal("pw_1", Assert.IsType<PaywallInfo>(payload).Identifier);
    }

    [Fact]
    public void Delegate_CustomAction_ParsesName() =>
        Assert.Equal("open_help", BridgeJson.ParseDelegatePayload("customPaywallAction", """{"name":"open_help"}"""));

    [Theory]
    [InlineData("paywallWillOpenUrl", "https://superwall.com/terms")]
    [InlineData("paywallWillOpenDeepLink", "camity://settings")]
    public void Delegate_UrlKinds_ParseUri(string kind, string url)
    {
        var payload = BridgeJson.ParseDelegatePayload(kind, $$"""{"url":"{{url}}"}""");

        Assert.Equal(new Uri(url), Assert.IsType<Uri>(payload));
    }

    [Fact]
    public void Delegate_SubscriptionStatus_ParsesNewStatus()
    {
        var payload = BridgeJson.ParseDelegatePayload("subscriptionStatusDidChange", """{"status":"active","activeEntitlementIds":["pro"]}""");

        var status = Assert.IsType<SubscriptionStatusInfo>(payload);
        Assert.Equal(SuperwallSubscriptionState.Active, status.Status);
        Assert.Equal(["pro"], status.ActiveEntitlementIds);
    }

    [Fact]
    public void Delegate_UnknownKind_IsIgnored() =>
        Assert.Null(BridgeJson.ParseDelegatePayload("somethingNew", """{"x":1}"""));

    [Fact]
    public void UserAttributes_SerializeWithNullAsRemoval()
    {
        var json = BridgeJson.SerializeUserAttributes(new Dictionary<string, object?> { ["plan"] = "pro", ["age"] = 30, ["beta"] = true, ["old"] = null });

        Assert.Equal("""{"plan":"pro","age":30,"beta":true,"old":null}""", json);
        Assert.Throws<ArgumentException>(() => BridgeJson.SerializeUserAttributes(new Dictionary<string, object?> { ["x"] = new object() }));
    }

    [Fact]
    public void UserAttributes_ParsePrimitives()
    {
        var attributes = BridgeJson.ParseUserAttributes(
            """{"plan":"pro","age":30,"score":1.5,"beta":true,"gone":null,"applicationInstalledAt":"2026-11-04T10:00:00Z"}""");

        Assert.Equal("pro", attributes["plan"]);
        Assert.Equal(30L, attributes["age"]);
        Assert.Equal(1.5, attributes["score"]);
        Assert.Equal(true, attributes["beta"]);
        Assert.Null(attributes["gone"]);
        Assert.Equal("2026-11-04T10:00:00Z", attributes["applicationInstalledAt"]);
    }

    [Fact]
    public void PresentationResult_MapsAllTypes()
    {
        Assert.IsType<PresentationResult.PlacementNotFound>(BridgeJson.ParsePresentationResult("""{"type":"placementNotFound"}"""));
        Assert.IsType<PresentationResult.NoAudienceMatch>(BridgeJson.ParsePresentationResult("""{"type":"noAudienceMatch"}"""));
        Assert.IsType<PresentationResult.PaywallNotAvailable>(BridgeJson.ParsePresentationResult("""{"type":"paywallNotAvailable"}"""));
        Assert.Equal(
            new PresentationResult.Paywall(new Experiment("e1", "g1", "v1", "pw_1")),
            BridgeJson.ParsePresentationResult("""{"type":"paywall","experiment":{"id":"e1","groupId":"g1","variantId":"v1","paywallId":"pw_1"}}"""));
        Assert.Equal(
            new PresentationResult.Holdout(new Experiment("e1", "g1", "v2", null)),
            BridgeJson.ParsePresentationResult("""{"type":"holdout","experiment":{"id":"e1","groupId":"g1","variantId":"v2","paywallId":null}}"""));
        Assert.Throws<JsonException>(() => BridgeJson.ParsePresentationResult("""{"type":"other"}"""));
    }

    [Fact]
    public void PresentationResult_Error_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BridgeJson.ParsePresentationResult("""{"type":"error","error":"Superwall is not configured"}"""));

        Assert.Equal("Superwall is not configured", ex.Message);
    }

    [Fact]
    public void Placements_SerializeAsArray() =>
        Assert.Equal("""["a","b"]""", BridgeJson.SerializePlacements(["a", "b"]));
}

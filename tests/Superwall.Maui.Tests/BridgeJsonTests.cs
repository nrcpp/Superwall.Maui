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

    [Theory]
    [InlineData(SuperwallLogLevel.Debug, "debug")]
    [InlineData(SuperwallLogLevel.Warn, "warn")]
    [InlineData(SuperwallLogLevel.None, "none")]
    public void Options_SerializeLogLevel(SuperwallLogLevel level, string wire) =>
        Assert.Equal($$"""{"logLevel":"{{wire}}"}""", BridgeJson.SerializeOptions(new SuperwallOptions { LogLevel = level }));

    [Fact]
    public void Params_SerializeFlatPrimitives()
    {
        var json = BridgeJson.SerializeParams(new Dictionary<string, object> { ["s"] = "v", ["b"] = true, ["n"] = 3 });

        Assert.Equal("""{"s":"v","b":true,"n":3}""", json);
        Assert.Null(BridgeJson.SerializeParams(null));
        Assert.Throws<ArgumentException>(() => BridgeJson.SerializeParams(new Dictionary<string, object> { ["x"] = new object() }));
    }
}

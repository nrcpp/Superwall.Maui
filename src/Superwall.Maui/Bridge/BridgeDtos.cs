using System.Text.Json.Serialization;

namespace Superwall.Maui.Bridge;

// JSON contract v1 of the native bridge: docs/architecture.md §4.4.

internal sealed class OptionsDto
{
    public string LogLevel { get; set; } = "warn";
    public string TestModeBehavior { get; set; } = "automatic";
}

internal sealed class PaywallInfoDto
{
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? PresentedByPlacementWithName { get; set; }
    public List<string>? ProductIds { get; set; }
}

internal sealed class PaywallResultDto
{
    public string? Type { get; set; }
    public string? ProductId { get; set; }
}

internal sealed class SkipReasonDto
{
    public string? Type { get; set; }
}

internal sealed class EventDto
{
    public string? Name { get; set; }
    public Dictionary<string, string?>? Params { get; set; }
}

internal sealed class SubscriptionStatusDto
{
    public string? Status { get; set; }
    public List<string>? ActiveEntitlementIds { get; set; }
}

internal sealed class RestorePurchasesResultDto
{
    public string? Type { get; set; }
    public string? Error { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OptionsDto))]
[JsonSerializable(typeof(PaywallInfoDto))]
[JsonSerializable(typeof(PaywallResultDto))]
[JsonSerializable(typeof(SkipReasonDto))]
[JsonSerializable(typeof(EventDto))]
[JsonSerializable(typeof(SubscriptionStatusDto))]
[JsonSerializable(typeof(RestorePurchasesResultDto))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext
{
}

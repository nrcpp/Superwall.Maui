using System.Text.Json.Serialization;

namespace Superwall.Maui.Bridge;

// JSON contract v1 of the native bridge: docs/architecture.md §4.4.

internal sealed class OptionsDto
{
    public string LogLevel { get; set; } = "warn";
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

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OptionsDto))]
[JsonSerializable(typeof(PaywallInfoDto))]
[JsonSerializable(typeof(PaywallResultDto))]
[JsonSerializable(typeof(SkipReasonDto))]
[JsonSerializable(typeof(EventDto))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext
{
}

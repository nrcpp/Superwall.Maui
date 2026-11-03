using System.Text;
using System.Text.Json;

namespace Superwall.Maui.Bridge;

/// <summary>Converts between the bridge JSON contract and the public C# types.</summary>
internal static class BridgeJson
{
    public static string SerializeOptions(SuperwallOptions options) =>
        JsonSerializer.Serialize(
            new OptionsDto { LogLevel = ToWire(options.LogLevel) },
            BridgeJsonContext.Default.OptionsDto);

    public static string? SerializeParams(IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in parameters)
            {
                switch (value)
                {
                    case string s: writer.WriteString(key, s); break;
                    case bool b: writer.WriteBoolean(key, b); break;
                    case int i: writer.WriteNumber(key, i); break;
                    case long l: writer.WriteNumber(key, l); break;
                    case float f: writer.WriteNumber(key, f); break;
                    case double d: writer.WriteNumber(key, d); break;
                    case decimal m: writer.WriteNumber(key, m); break;
                    default:
                        throw new ArgumentException(
                            $"Placement parameter '{key}' has unsupported type '{value?.GetType().Name ?? "null"}'. Supported: string, bool, number.",
                            nameof(parameters));
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static PaywallInfo ParsePaywallInfo(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.PaywallInfoDto)
            ?? throw new JsonException("paywallInfoJson is null");
        return new PaywallInfo(
            dto.Identifier ?? string.Empty,
            dto.Name ?? string.Empty,
            dto.Url ?? string.Empty,
            dto.PresentedByPlacementWithName,
            dto.ProductIds ?? []);
    }

    public static PaywallResult ParsePaywallResult(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.PaywallResultDto)
            ?? throw new JsonException("resultJson is null");
        return dto.Type switch
        {
            "purchased" => new PaywallResult.Purchased(dto.ProductId ?? string.Empty),
            "restored" => new PaywallResult.Restored(),
            "declined" => new PaywallResult.Declined(),
            _ => throw new JsonException($"Unknown paywall result type '{dto.Type}'"),
        };
    }

    public static PaywallSkippedReason ParseSkipReason(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.SkipReasonDto);
        return dto?.Type switch
        {
            "holdout" => PaywallSkippedReason.Holdout,
            "noAudienceMatch" => PaywallSkippedReason.NoAudienceMatch,
            "placementNotFound" => PaywallSkippedReason.PlacementNotFound,
            "userIsSubscribed" => PaywallSkippedReason.UserIsSubscribed,
            _ => PaywallSkippedReason.Unknown,
        };
    }

    public static SuperwallEventInfo ParseEvent(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.EventDto)
            ?? throw new JsonException("eventJson is null");
        return new SuperwallEventInfo(
            dto.Name ?? string.Empty,
            dto.Params ?? new Dictionary<string, string?>());
    }

    private static string ToWire(SuperwallLogLevel level) => level switch
    {
        SuperwallLogLevel.Debug => "debug",
        SuperwallLogLevel.Info => "info",
        SuperwallLogLevel.Warn => "warn",
        SuperwallLogLevel.Error => "error",
        SuperwallLogLevel.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}

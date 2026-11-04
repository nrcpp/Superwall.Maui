using System.Text;
using System.Text.Json;

namespace Superwall.Maui.Bridge;

/// <summary>Converts between the bridge JSON contract and the public C# types.</summary>
internal static class BridgeJson
{
    public static string SerializeOptions(SuperwallOptions options) =>
        JsonSerializer.Serialize(
            new OptionsDto
            {
                LogLevel = ToWire(options.LogLevel),
                TestModeBehavior = ToWire(options.TestModeBehavior),
                LocaleIdentifier = options.LocaleIdentifier,
                EventTrackingBehavior = options.EventTrackingBehavior is { } tracking ? ToWire(tracking) : null,
                Paywalls = ToDto(options.Paywalls),
            },
            BridgeJsonContext.Default.OptionsDto);

    public static string? SerializeParams(IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        return WriteObject(writer =>
        {
            foreach (var (key, value) in parameters)
            {
                if (value is null || !TryWritePrimitive(writer, key, value))
                {
                    throw new ArgumentException(
                        $"Placement parameter '{key}' has unsupported type '{value?.GetType().Name ?? "null"}'. Supported: string, bool, number.",
                        nameof(parameters));
                }
            }
        });
    }

    public static string SerializeUserAttributes(IReadOnlyDictionary<string, object?> attributes) =>
        WriteObject(writer =>
        {
            foreach (var (key, value) in attributes)
            {
                if (value is null)
                {
                    writer.WriteNull(key);
                }
                else if (!TryWritePrimitive(writer, key, value))
                {
                    throw new ArgumentException(
                        $"User attribute '{key}' has unsupported type '{value.GetType().Name}'. Supported: string, bool, number, null.",
                        nameof(attributes));
                }
            }
        });

    public static IReadOnlyDictionary<string, object?> ParseUserAttributes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, object?>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var value = property.Value;
            result[property.Name] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.TryGetInt64(out var l) ? (object)l : value.GetDouble(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.GetRawText(),
            };
        }

        return result;
    }

    public static string SerializePlacements(IEnumerable<string> placements) =>
        JsonSerializer.Serialize(placements.ToArray(), BridgeJsonContext.Default.StringArray);

    public static PresentationResult ParsePresentationResult(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.PresentationResultDto)
            ?? throw new JsonException("presentationResultJson is null");
        return dto.Type switch
        {
            "placementNotFound" => new PresentationResult.PlacementNotFound(),
            "noAudienceMatch" => new PresentationResult.NoAudienceMatch(),
            "paywallNotAvailable" => new PresentationResult.PaywallNotAvailable(),
            "paywall" => new PresentationResult.Paywall(ToExperiment(dto.Experiment)),
            "holdout" => new PresentationResult.Holdout(ToExperiment(dto.Experiment)),
            "error" => throw new InvalidOperationException(dto.Error ?? "Superwall could not evaluate the placement."),
            _ => throw new JsonException($"Unknown presentation result type '{dto.Type}'"),
        };
    }

    /// <summary>
    /// Parses a delegate payload: <see cref="PaywallInfo"/>, <see cref="string"/> (custom action), <see cref="Uri"/>
    /// or <see cref="SubscriptionStatusInfo"/>. Returns null for unknown kinds (forward-compatible).
    /// </summary>
    public static object? ParseDelegatePayload(string kind, string json) => kind switch
    {
        "willPresentPaywall" or "didPresentPaywall" or "willDismissPaywall" or "didDismissPaywall" => ParsePaywallInfo(json),
        "customPaywallAction" => JsonSerializer.Deserialize(json, BridgeJsonContext.Default.CustomActionDto)?.Name
            ?? throw new JsonException("customPaywallAction payload has no name"),
        "paywallWillOpenUrl" or "paywallWillOpenDeepLink" => ParseUrl(json),
        "subscriptionStatusDidChange" => ParseSubscriptionStatus(json),
        _ => null,
    };

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

    public static SubscriptionStatusInfo ParseSubscriptionStatus(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.SubscriptionStatusDto)
            ?? throw new JsonException("subscriptionStatusJson is null");
        var status = dto.Status switch
        {
            "active" => SuperwallSubscriptionState.Active,
            "inactive" => SuperwallSubscriptionState.Inactive,
            _ => SuperwallSubscriptionState.Unknown,
        };
        return new SubscriptionStatusInfo(status, dto.ActiveEntitlementIds ?? []);
    }

    public static RestorePurchasesResult ParseRestoreResult(string json)
    {
        var dto = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.RestorePurchasesResultDto)
            ?? throw new JsonException("restoreResultJson is null");
        return dto.Type switch
        {
            "restored" => new RestorePurchasesResult(true, null),
            "failed" => new RestorePurchasesResult(false, dto.Error),
            _ => throw new JsonException($"Unknown restore result type '{dto.Type}'"),
        };
    }

    private static string WriteObject(Action<Utf8JsonWriter> writeProperties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool TryWritePrimitive(Utf8JsonWriter writer, string key, object value)
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
            default: return false;
        }

        return true;
    }

    private static PaywallOptionsDto? ToDto(SuperwallPaywallOptions paywalls) =>
        paywalls is { IsHapticFeedbackEnabled: null, ShouldShowPurchaseFailureAlert: null, ShouldPreload: null, AutomaticallyDismiss: null }
            ? null
            : new PaywallOptionsDto
            {
                IsHapticFeedbackEnabled = paywalls.IsHapticFeedbackEnabled,
                ShouldShowPurchaseFailureAlert = paywalls.ShouldShowPurchaseFailureAlert,
                ShouldPreload = paywalls.ShouldPreload,
                AutomaticallyDismiss = paywalls.AutomaticallyDismiss,
            };

    private static Experiment ToExperiment(ExperimentDto? dto) => new(
        dto?.Id ?? string.Empty,
        dto?.GroupId ?? string.Empty,
        dto?.VariantId ?? string.Empty,
        dto?.PaywallId);

    private static Uri ParseUrl(string json)
    {
        var url = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.UrlDto)?.Url;
        return url is not null && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri)
            ? uri
            : throw new JsonException($"Invalid url payload '{json}'");
    }

    internal static string ToWire(SuperwallLogLevel level) => level switch
    {
        SuperwallLogLevel.Debug => "debug",
        SuperwallLogLevel.Info => "info",
        SuperwallLogLevel.Warn => "warn",
        SuperwallLogLevel.Error => "error",
        SuperwallLogLevel.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    private static string ToWire(SuperwallTestModeBehavior behavior) => behavior switch
    {
        SuperwallTestModeBehavior.Automatic => "automatic",
        SuperwallTestModeBehavior.WhenEnabledForUser => "whenEnabledForUser",
        SuperwallTestModeBehavior.Never => "never",
        SuperwallTestModeBehavior.Always => "always",
        _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null),
    };

    private static string ToWire(SuperwallEventTrackingBehavior behavior) => behavior switch
    {
        SuperwallEventTrackingBehavior.All => "all",
        SuperwallEventTrackingBehavior.SuperwallOnly => "superwallOnly",
        SuperwallEventTrackingBehavior.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null),
    };
}

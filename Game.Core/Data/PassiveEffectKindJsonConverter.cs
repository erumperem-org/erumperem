using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Passives;

namespace Game.Core.Data;

/// <summary>
/// Accepts legacy <c>effectKind</c> strings from older passives.json (e.g. after enum renames).
/// </summary>
public sealed class PassiveEffectKindJsonConverter : JsonConverter<PassiveEffectKind>
{
    private static readonly Dictionary<string, PassiveEffectKind> LegacyNamesToKind =
        new(StringComparer.Ordinal)
        {
            ["ExtraTokenOnSelfSkillWhenRank"] = PassiveEffectKind.ExtraTokenOnSelfSkill,
        };

    public override PassiveEffectKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for {nameof(PassiveEffectKind)}, got {reader.TokenType}.");
        }

        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw))
        {
            throw new JsonException($"{nameof(PassiveEffectKind)} value is empty.");
        }

        if (LegacyNamesToKind.TryGetValue(raw, out var legacyKind))
        {
            return legacyKind;
        }

        if (Enum.TryParse(raw, ignoreCase: false, out PassiveEffectKind parsed))
        {
            return parsed;
        }

        throw new JsonException($"Unknown {nameof(PassiveEffectKind)}: \"{raw}\".");
    }

    public override void Write(Utf8JsonWriter writer, PassiveEffectKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

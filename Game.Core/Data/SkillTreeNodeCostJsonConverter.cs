using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Data;

/// <summary>
/// Accepts skill-tree node cost as JSON number or string (e.g. <c>"2"</c>).
/// </summary>
public sealed class SkillTreeNodeCostJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (!reader.TryGetInt32(out var wholeNumber))
                {
                    throw new JsonException("Skill tree node cost must be a whole number (int).");
                }

                ValidateNonNegative(wholeNumber);
                return wholeNumber;
            case JsonTokenType.String:
                var rawString = reader.GetString();
                if (string.IsNullOrWhiteSpace(rawString))
                {
                    throw new JsonException("Skill tree node cost string cannot be empty.");
                }

                if (!int.TryParse(rawString.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new JsonException(
                        $"Skill tree node cost could not parse as integer from string '{rawString}'.");
                }

                ValidateNonNegative(parsed);
                return parsed;
            default:
                throw new JsonException($"Unexpected JSON token for skill tree node cost: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static void ValidateNonNegative(int cost)
    {
        if (cost < 0)
        {
            throw new JsonException($"Skill tree node cost must be non-negative. Received {cost}.");
        }
    }
}

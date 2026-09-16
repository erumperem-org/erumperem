using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;

namespace Game.Core.Data;

/// <summary>Rejects obsolete Enemy/Ally names so authors use OneEnemy/OneAlly.</summary>
internal sealed class SkillTargetKindJsonConverter : JsonConverter<SkillTargetKind>
{
    public override SkillTargetKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawValue = reader.GetString();
        if (string.Equals(rawValue, "Enemy", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("Skill targetKind 'Enemy' is obsolete. Use OneEnemy.");
        }

        if (string.Equals(rawValue, "Ally", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("Skill targetKind 'Ally' is obsolete. Use OneAlly.");
        }

        if (Enum.TryParse(rawValue, ignoreCase: true, out SkillTargetKind parsedTargetKind) &&
            Enum.IsDefined(typeof(SkillTargetKind), parsedTargetKind))
        {
            return parsedTargetKind;
        }

        throw new JsonException($"Unknown skill targetKind '{rawValue}'.");
    }

    public override void Write(Utf8JsonWriter writer, SkillTargetKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

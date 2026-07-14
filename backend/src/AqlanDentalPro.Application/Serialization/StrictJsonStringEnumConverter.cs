using System.Text.Json;
using System.Text.Json.Serialization;

namespace AqlanDentalPro.Application.Serialization;

public sealed class StrictJsonStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"{typeof(TEnum).Name} must be a string enum value.");

        var raw = reader.GetString();
        if (raw is null
            || !Enum.TryParse<TEnum>(raw, ignoreCase: false, out var value)
            || !string.Equals(Enum.GetName(value), raw, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported {typeof(TEnum).Name} value.");
        }

        return value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value))
            throw new JsonException($"Unsupported {typeof(TEnum).Name} value.");

        writer.WriteStringValue(value.ToString());
    }
}

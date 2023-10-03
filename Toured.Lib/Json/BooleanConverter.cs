using System.Text.Json;
using System.Text.Json.Serialization;

namespace TourEd.Lib.Json;

public class BooleanConverter : JsonConverter<bool>
{
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
    
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) ? b : throw new JsonException(),
        JsonTokenType.Number => reader.TryGetInt64(out var l) ? Convert.ToBoolean(l) : reader.TryGetDouble(out var d) && Convert.ToBoolean(d),
        _ => throw new JsonException($"Kann nicht konvertiert werden: { reader.TokenType }"),
    };
}

public class FalseOrNullConverter<T> : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.False) return default;

        if (options.GetConverter(typeof(JsonElement)) is JsonConverter<JsonElement> converter) {
            var json = converter.Read(ref reader, typeToConvert, options).GetRawText();
            return JsonSerializer.Deserialize<T?>(json);
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            JsonSerializer.Serialize(writer, false, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

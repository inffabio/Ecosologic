using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ecosologic.Api;

public readonly struct Optional<T> where T : struct
{
    private readonly T? _value;

    public Optional(T? value)
    {
        _value = value;
        HasValue = true;
    }

    public T? Value => _value;
    public bool HasValue { get; }

    public static Optional<T> Absent() => default;
}

public sealed class OptionalDateTimeConverter : JsonConverter<Optional<DateTimeOffset>>
{
    public override Optional<DateTimeOffset> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new Optional<DateTimeOffset>(null);
        return new Optional<DateTimeOffset>(reader.GetDateTimeOffset());
    }

    public override void Write(Utf8JsonWriter writer, Optional<DateTimeOffset> value, JsonSerializerOptions options)
    {
        if (value.HasValue && value.Value is { } dateTimeOffset)
            writer.WriteStringValue(dateTimeOffset);
        else
            writer.WriteNullValue();
    }
}

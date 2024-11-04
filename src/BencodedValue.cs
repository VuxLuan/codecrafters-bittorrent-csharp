using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace codecrafters_bittorrent;


[JsonConverter(typeof(BencodedValueJsonConverter))]
public abstract record BencodedValue
{
    public required ArraySegment<byte> OriginalData { get; init; }
    public required object Value { get; init; }
}

public record BencodedString : BencodedValue
{
    private ArraySegment<byte> Bytes => (ArraySegment<byte>)Value;
    public string Str => Encoding.UTF8.GetString(Bytes);
}

public record BencodedInt : BencodedValue
{
    public long Int => (long)Value;
}

public record BencodedList : BencodedValue
{
    public List<BencodedValue> List => (List<BencodedValue>)Value;
}

public record BencodedDict : BencodedValue
{
    public Dictionary<string, BencodedValue> Dict => (Dictionary<string, BencodedValue>)Value;
}

public class BencodedValueJsonConverter : JsonConverter<BencodedValue>
{
    public override BencodedValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, BencodedValue value, JsonSerializerOptions options)
    {
        if (value is BencodedString str)
        {
            writer.WriteStringValue(str.Str);
        }
        else
        {
            writer.WriteRawValue(JsonSerializer.Serialize(value.Value, options));
        }
    }
}

public class BencodeException : Exception
{
    public BencodeException(string message) : base(message)
    {
    }

    public BencodeException(string message, Exception inner) : base(message, inner)
    {
    }
}


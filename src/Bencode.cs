using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace codecrafters_bittorrent;

public class BencodeException : Exception
{
    public BencodeException(string message) : base(message) { }
    public BencodeException(string message, Exception inner) : base(message, inner) { }
}

[JsonConverter(typeof(BencodedValueJsonConverter))]
public abstract record BencodedValue
{
    public required ArraySegment<byte> OriginalData { get; init; }
    public required object Value { get; init; }

    public virtual T? As<T>() where T : BencodedValue => this as T;

    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        if (this is BencodedString str)
        {
            value = str.Str;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetInt(out long value)
    {
        if (this is BencodedInt intVal)
        {
            value = intVal.Int;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryGetList([NotNullWhen(true)] out List<BencodedValue>? value)
    {
        if (this is BencodedList listVal)
        {
            value = listVal.List;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetDict([NotNullWhen(true)] out Dictionary<string, BencodedValue>? value)
    {
        if (this is BencodedDict dictVal)
        {
            value = dictVal.Dict;
            return true;
        }
        value = null;
        return false;
    }
}

public record BencodedString : BencodedValue
{
    public ArraySegment<byte> Bytes => (ArraySegment<byte>)Value;
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
    public override BencodedValue Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, BencodedValue value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case BencodedString str:
                writer.WriteStringValue(str.Str);
                break;
            case BencodedInt intVal:
                writer.WriteNumberValue(intVal.Int);
                break;
            case BencodedList listVal:
                writer.WriteStartArray();
                foreach (var item in listVal.List)
                    JsonSerializer.Serialize(writer, item, options);
                writer.WriteEndArray();
                break;
            case BencodedDict dictVal:
                writer.WriteStartObject();
                foreach (var (key, val) in dictVal.Dict)
                {
                    writer.WritePropertyName(key);
                    JsonSerializer.Serialize(writer, val, options);
                }
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Unsupported type: {value.GetType()}");
        }
    }
}

public static class Bencode
{
    public static BencodedValue Decode(ArraySegment<byte> encodedValue)
    {
        if (encodedValue.Count == 0)
            throw new ArgumentException("Empty input", nameof(encodedValue));

        return (char)encodedValue[0] switch
        {
            var c when char.IsDigit(c) => DecodeString(encodedValue),
            'i' => DecodeInteger(encodedValue),
            'l' => DecodeList(encodedValue),
            'd' => DecodeDict(encodedValue),
            _ => throw new BencodeException($"Invalid prefix at offset {encodedValue.Offset}")
        };
    }

    private static BencodedString DecodeString(ArraySegment<byte> encodedValue)
    {
        var colonIndex = Array.IndexOf(encodedValue.Array!, (byte)':', encodedValue.Offset, encodedValue.Count);
        if (colonIndex == -1)
            throw new BencodeException("Invalid string format: missing colon");

        var lengthStr = Encoding.UTF8.GetString(encodedValue[..(colonIndex - encodedValue.Offset)]);
        if (!int.TryParse(lengthStr, out var strLength))
            throw new BencodeException("Invalid string length");

        try
        {
            var stringBytes = encodedValue.Slice(colonIndex + 1 - encodedValue.Offset, strLength);
            return new BencodedString
            {
                OriginalData = encodedValue[..(colonIndex + strLength + 1 - encodedValue.Offset)],
                Value = stringBytes
            };
        }
        catch (ArgumentException)
        {
            throw new BencodeException("String length exceeds available data");
        }
    }

    private static BencodedInt DecodeInteger(ArraySegment<byte> encodedValue)
    {
        var end = Array.IndexOf(encodedValue.Array!, (byte)'e', encodedValue.Offset, encodedValue.Count);
        if (end == -1)
            throw new BencodeException("Invalid integer format: missing 'e'");

        var numberStr = Encoding.UTF8.GetString(encodedValue.Slice(1, end - encodedValue.Offset - 1));
        if (!long.TryParse(numberStr, out var number))
            throw new BencodeException("Invalid integer value");

        return new BencodedInt
        {
            OriginalData = encodedValue[..(end + 1 - encodedValue.Offset)],
            Value = number
        };
    }

    private static BencodedList DecodeList(ArraySegment<byte> encodedValue)
    {
        var list = new List<BencodedValue>();
        
        // Handle empty list
        if (encodedValue.Count > 1 && encodedValue[1] == 'e')
        {
            return new BencodedList
            {
                OriginalData = encodedValue[..2],
                Value = list
            };
        }

        var index = 1;
        while (true)
        {
            if (index >= encodedValue.Count)
                throw new BencodeException("Unexpected end of list data");

            if (encodedValue[index] == 'e')
                break;

            var item = Decode(encodedValue[index..]);
            list.Add(item);
            index += item.OriginalData.Count;
        }

        return new BencodedList
        {
            OriginalData = encodedValue[..(index + 1)],
            Value = list
        };
    }

    private static BencodedDict DecodeDict(ArraySegment<byte> encodedValue)
    {
        var dict = new Dictionary<string, BencodedValue>();

        // Handle empty dictionary
        if (encodedValue.Count > 1 && encodedValue[1] == 'e')
        {
            return new BencodedDict
            {
                OriginalData = encodedValue[..2],
                Value = dict
            };
        }

        var index = 1;
        while (true)
        {
            if (index >= encodedValue.Count)
                throw new BencodeException("Unexpected end of dictionary data");

            if (encodedValue[index] == 'e')
                break;

            var keyValue = Decode(encodedValue[index..]);
            if (keyValue is not BencodedString keyString)
                throw new BencodeException("Dictionary key must be a string");

            index += keyString.OriginalData.Count;

            if (index >= encodedValue.Count)
                throw new BencodeException("Missing dictionary value");

            var value = Decode(encodedValue[index..]);
            dict[keyString.Str] = value;
            index += value.OriginalData.Count;
        }

        return new BencodedDict
        {
            OriginalData = encodedValue[..(index + 1)],
            Value = dict
        };
    }
}
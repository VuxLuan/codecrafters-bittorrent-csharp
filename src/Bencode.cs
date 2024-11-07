using System.Text;

namespace codecrafters_bittorrent;


public static class Bencode
{
    public static BencodedValue Decode(ArraySegment<byte> encodedValue)
    {
        // Create a span for efficient processing
        ReadOnlySpan<byte> span = encodedValue;
        if (span.IsEmpty) throw new ArgumentException("Empty input", nameof(encodedValue));

        return (char)span[0] switch
        {
            var c when char.IsDigit(c) => DecodeString(encodedValue, span),
            'i' => DecodeInteger(encodedValue, span),
            'l' => DecodeList(encodedValue, span),
            'd' => DecodeDict(encodedValue, span),
            _ => throw new BencodeException($"Invalid prefix")
        };
    }

    private static BencodedString DecodeString(ArraySegment<byte> original, ReadOnlySpan<byte> span)
    {
        var colonIndex = span.IndexOf((byte)':');
        if (colonIndex == -1) throw new BencodeException("Invalid string format: missing colon");

        if (!int.TryParse(Encoding.UTF8.GetString(span[..colonIndex]), out var strLength))
            throw new BencodeException("Invalid string length");

        try
        {
            var stringBytes = original.Slice(colonIndex + 1, strLength);
            return new BencodedString
            {
                OriginalData = original[..(colonIndex + strLength + 1)], 
                Value = stringBytes
            };
        }
        catch (ArgumentException)
        {
            throw new BencodeException("String length exceeds available data");
        }
    }

    private static BencodedInt DecodeInteger(ArraySegment<byte> original, ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)'e');
        if (end == -1) throw new BencodeException("Invalid integer format: missing 'e'");

        var numberStr = Encoding.UTF8.GetString(span.Slice(1, end - 1));
        if (!long.TryParse(numberStr, out var number)) throw new BencodeException("Invalid integer value");

        return new BencodedInt { OriginalData = original[..(end + 1)], Value = number };
    }

    private static BencodedList DecodeList(ArraySegment<byte> original, ReadOnlySpan<byte> span)
    {
        var list = new List<BencodedValue>();

        if (span.Length > 1 && span[1] == 'e')
        {
            return new BencodedList { OriginalData = original[..2], Value = list };
        }

        var index = 1;
        while (true)
        {
            if (index >= span.Length) throw new BencodeException("Unexpected end of list data");

            if (span[index] == 'e') break;

            var item = Decode(original[index..]);
            list.Add(item);
            index += item.OriginalData.Count;
        }

        return new BencodedList { OriginalData = original[..(index + 1)], Value = list };
    }

    private static BencodedDict DecodeDict(ArraySegment<byte> original, ReadOnlySpan<byte> span)
    {
        var dict = new Dictionary<string, BencodedValue>();

        if (span.Length > 1 && span[1] == 'e')
        {
            return new BencodedDict { OriginalData = original[..2], Value = dict };
        }

        var index = 1;
        while (true)
        {
            if (index >= span.Length) throw new BencodeException("Unexpected end of dictionary data");

            if (span[index] == 'e') break;

            var keyValue = Decode(original[index..]);
            if (keyValue is not BencodedString keyString) throw new BencodeException("Dictionary key must be a string");

            index += keyString.OriginalData.Count;

            if (index >= span.Length) throw new BencodeException("Missing dictionary value");

            var value = Decode(original[index..]);
            dict[keyString.Str] = value;
            index += value.OriginalData.Count;
        }

        return new BencodedDict { OriginalData = original[..(index + 1)], Value = dict };
    }
}
namespace codecrafters_bittorrent;

public static class BEncoding
{
    
    
    public static object Decode(string encodedValue)
    {
        if (Char.IsDigit(encodedValue[0]))
        {
            return BDecodeStr(encodedValue);
        }
        if (encodedValue[0] == 'i')
        {
            return BDecodeInt(encodedValue);
        }
        if (encodedValue[0] == 'l')
        {
            return BDecodeList(encodedValue);
        }

        if (encodedValue[0] == 'd')
        {
            return BDecodeDict(encodedValue);
        }
        {
            throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
        }
    }

    private static string EnCode(object input)
    {
        return Type.GetTypeCode(input.GetType())
            switch
            {
                TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => $"i{input}e",
                TypeCode.String => $"{((string)input).Length}:{input}",
                TypeCode.Object => input switch
                {
                    object[] inputArray => $"l{string.Join("", inputArray.Select(x => EnCode(x)))}e",
                    
                    SortedDictionary<string, object> inputDictionary =>
                        $"d{string.Join("", inputDictionary.Select(kvp => $"{EnCode(kvp.Key)}{EnCode(kvp.Value)}"))}",
                    
                    _ => throw new Exception($"Unknown object type: {input.GetType().FullName}")
                },
                _ => throw new Exception($"Unknown type:{input.GetType().FullName}")
            };
    }
    
    private static string BDecodeStr(string encodedValue)
    {
        var colonIndex = encodedValue.IndexOf(':');
        if (colonIndex == -1) throw new InvalidOperationException("Invalid encoded value: " + encodedValue);
        var strLength = int.Parse(encodedValue[..colonIndex]);
        var strValue = encodedValue.Substring(colonIndex + 1, strLength);
        return strValue;
    
    }
    
    private static long BDecodeInt(string encodedValue)
    {
        var iIndex = encodedValue.IndexOf('i');
        var eIndex = encodedValue.IndexOf('e');
        
        var intLength = eIndex - iIndex;
        var intValue = encodedValue.Substring(iIndex + 1, intLength-1);
        return long.Parse(intValue);
    
    }
    
    private static object[] BDecodeList(string encodedValue)
    {
        encodedValue = encodedValue[1..];
        var results = new List<object>();
        while (encodedValue.Length > 0 && encodedValue[0] != 'e')
        {
            var element = Decode(encodedValue);
            results.Add(element);
            encodedValue = encodedValue[EnCode(element).Length..];

        }
        return results.ToArray();
    }
    

    private static SortedDictionary<string, object> BDecodeDict(string encodedValue)
    {
        encodedValue = encodedValue[1..];
        var results = new SortedDictionary<string, object>();
        while (encodedValue.Length > 0 && encodedValue[0] != 'e')
        {
            var key = BDecodeStr(encodedValue);
            encodedValue = encodedValue[(EnCode(key).Length)..];
            var value = Decode(encodedValue);
            results.Add(key, value);
            encodedValue = encodedValue[EnCode(value).Length..];
        }
        
        return results;
    }
    
}
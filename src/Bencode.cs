namespace codecrafters_bittorrent;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
public static class Bencode {
  public static BencodedValue Decode(ArraySegment<byte> encodedValue) {
    var firstChar = (char)encodedValue[0];
    var valueAsList = (IList<byte>)encodedValue;
    if (char.IsDigit(firstChar)) // string
    {
      // Example: "5:hello" -> "hello"
      var colonIndex = valueAsList.IndexOf((byte)':');
      if (colonIndex != -1) {
        var strLength = int.Parse(encodedValue[..colonIndex]);
        try {
          var stringBytes = encodedValue.Slice(colonIndex + 1, strLength);
          return new BencodedString {
            OriginalData = encodedValue[..(colonIndex + strLength + 1)],
            Value = stringBytes,
          };
        } catch {
          Console.WriteLine(
              $"Failed reading {encodedValue.ToString()}[{colonIndex + 1}, {strLength}]");
          throw;
        }
      }
    } else switch (firstChar)
    {
      // int
      case 'i':
      {
        var end = valueAsList.IndexOf((byte)'e');
        if (long.TryParse(encodedValue[1..end], out var integer)) {
          return new BencodedInt {
            OriginalData = encodedValue[..(end + 1)],
            Value = integer,
          };
        }

        break;
      }
      // list
      case 'l':
      {
        var list = new List<BencodedValue>();
        if (encodedValue[1] == 'e') {
          return new BencodedList {
            OriginalData = encodedValue[..2],
            Value = list,
          };
        }
        var index = 1;
        while (true) {
          var listItem = Decode(encodedValue[index..^ 1]);
          index += listItem.OriginalData.Count;
          list.Add(listItem);
          if (index >= encodedValue.Count) {
            throw new Exception();
          }
          if (encodedValue[index] == 'e') {
            break;
          }
        }
        return new BencodedList {
          OriginalData = encodedValue[..(index + 1)],
          Value = list,
        };
      }
      // dict
      case 'd':
      {
        var dict = new Dictionary<string, BencodedValue>();
        if (encodedValue[1] == 'e') {
          return new BencodedDict {
            OriginalData = encodedValue[..2],
            Value = dict,
          };
        }
        var index = 1;
        while (true) {
          var key = (BencodedString)Decode(encodedValue[index..^ 1]);
          index += key.OriginalData.Count;
          var val = Decode(encodedValue[index..^ 1]);
          index += val.OriginalData.Count;
          dict[key.Str] = val;
          if (index >= encodedValue.Count) {
            throw new Exception();
          }
          if (encodedValue[index] == 'e') {
            break;
          }
        }
        return new BencodedDict {
          OriginalData = encodedValue[..(index + 1)],
          Value = dict,
        };
      }
    }
    throw new InvalidOperationException(
        $"Invalid encoded value at offset {encodedValue.Offset}");
  }
}
public class BencodedValueJsonConverter : JsonConverter<BencodedValue> {
  public override BencodedValue
  Read(ref Utf8JsonReader reader, Type typeToConvert,
       JsonSerializerOptions options) => throw new NotImplementedException();
  public override void Write(Utf8JsonWriter writer, BencodedValue value,
                             JsonSerializerOptions options) {
    if (value is BencodedString str) {
      writer.WriteStringValue(str.Str);
    } else {
      writer.WriteRawValue(JsonSerializer.Serialize(value.Value, options));
    }
  }
}
[JsonConverter(typeof(BencodedValueJsonConverter))]
public abstract class BencodedValue {
  public ArraySegment<byte> OriginalData;
  public object Value { get; init; }
}
public class BencodedString : BencodedValue {
  public ArraySegment<byte> Bytes => (ArraySegment<byte>)base.Value;
  public string Str => Encoding.UTF8.GetString(Bytes);
}
public class BencodedInt : BencodedValue {
  public long Int => (long)base.Value;
}
public class BencodedList : BencodedValue {
  public List<BencodedValue> List => (List<BencodedValue>)base.Value;
}
public class BencodedDict : BencodedValue {
  public Dictionary<string, BencodedValue> Dict =>
      (Dictionary<string, BencodedValue>)base.Value;
}
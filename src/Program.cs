using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using codecrafters_bittorrent;

// Parse arguments
var (command, param) = args.Length switch {
  0 => throw new InvalidOperationException(
      "Usage: your_bittorrent.sh <command> <param>"),
  1 => throw new InvalidOperationException(
      "Usage: your_bittorrent.sh <command> <param>"),
  _ => (args[0], args[1])
};
// Parse command and act accordingly
if (command == "decode") {
  var encodedValue = Encoding.UTF8.GetBytes(param);
  Console.WriteLine(JsonSerializer.Serialize(Bencode.Decode(encodedValue)));
} else if (command == "info") {
  var fileContents = File.ReadAllBytes(param);
  var dict = (BencodedDict)Bencode.Decode(fileContents);
  Console.WriteLine(
      $"Tracker URL: {((BencodedString)dict.Dict["announce"]).Str}");
  var infoDict = (BencodedDict)dict.Dict["info"];
  Console.WriteLine($"Length: {infoDict.Dict["length"].Value}");
  var infoHash = SHA1.HashData(infoDict.OriginalData);
  Console.WriteLine(
      $"Info Hash: {Convert.ToHexString(infoHash).ToLower().Replace("-","")}");
  Console.WriteLine($"Piece Length: {infoDict.Dict["piece length"].Value}");
  var pieceHashesBytes = (ArraySegment<byte>)infoDict.Dict["pieces"].Value;
  for (var i = 0; i < pieceHashesBytes.Count; i += 20) {
    var pieceHash = pieceHashesBytes.Slice(i, 20);
    Console.WriteLine(
        Convert.ToHexString(pieceHash).ToLower().Replace("-", ""));
  }
} else if (command == "peers") {
  var fileContents = File.ReadAllBytes(param);
  var dict = (BencodedDict)Bencode.Decode(fileContents);
  var trackerUrl = ((BencodedString)dict.Dict["announce"]).Str;
  var infoDict = (BencodedDict)dict.Dict["info"];
  var length = (long)infoDict.Dict["length"].Value;
  var infoHash = SHA1.HashData(infoDict.OriginalData);
  var httpClient = new HttpClient();
  var uriBuilder = new UriBuilder(trackerUrl);
  uriBuilder.Query =
      $"?info_hash={HttpUtility.UrlEncode(infoHash)}&peer_id=8933r3mkeo0kop3l004a&port=6881&uploaded=0&downloaded=0&left={length}&compact=1";
  var encodedResponse = await httpClient.GetByteArrayAsync(uriBuilder.Uri);
  var response = (BencodedDict)Bencode.Decode(encodedResponse);
  var peers = (ArraySegment<byte>)response.Dict["peers"].Value;
  for (var i = 0; i < peers.Count; i += 6) {
    var peerIpBytes = peers.Slice(i, 4);
    var peerPort = BitConverter.ToUInt16(peers.Slice(i + 4, 2));
    if (BitConverter.IsLittleEndian) {
      peerPort = BinaryPrimitives.ReverseEndianness(peerPort);
    }
    Console.WriteLine($"{new IPAddress(peerIpBytes)}:{peerPort}");
  }
} else if (command == "handshake") {
  /*
     length of the protocol string (BitTorrent protocol) which is 19 (1 byte)
     the string BitTorrent protocol (19 bytes)
     eight reserved bytes, which are all set to zero (8 bytes)
     sha1 infohash (20 bytes) (NOT the hexadecimal representation, which is 40
     bytes long) peer id (20 bytes) (generate 20 random byte values)
   */
  var fileContents = File.ReadAllBytes(param);
  var dict = (BencodedDict)Bencode.Decode(fileContents);
  var infoDict = (BencodedDict)dict.Dict["info"];
  var peerEndpoint = IPEndPoint.Parse(args[2]);
  var tcpClient = new TcpClient();
  await tcpClient.ConnectAsync(peerEndpoint);
  await using var stream = tcpClient.GetStream();
  var buffer = new byte[68];
  buffer[0] = 19;
  Encoding.UTF8.GetBytes("BitTorrent protocol", buffer.AsSpan()[1..]);
  SHA1.HashData(infoDict.OriginalData, buffer.AsSpan()[28..]);
  Random.Shared.NextBytes(buffer.AsSpan()[48..]);
  await stream.WriteAsync(buffer);
  await stream.FlushAsync();
  _ = await stream.ReadAsync(buffer);
  Console.WriteLine(
      $"Peer ID: {Convert.ToHexString(buffer, 48, 20).ToLower().Replace("-","")}");
} else {
  throw new InvalidOperationException($"Invalid command: {command}");
}
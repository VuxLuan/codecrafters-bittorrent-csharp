// using System.Text;
// using System.Text.Json;
//
// namespace codecrafters_bittorrent;
//
// public class TorrentFileParser
// {
//     public string Announce { get; private set; } = string.Empty;
//     public byte[]? InfoHash { get; private set; }
//     public List<byte[]> PieceHashes { get; } = [];
//     public int PieceLength { get; private set; }
//     public long Length { get; private set; }
//     public string Name { get; set; } = string.Empty;
//     
//
//     public async Task ParserAsync(string fileName)
//     {
//         if (!File.Exists(fileName))
//         {
//             throw new FileNotFoundException("File not found", fileName);
//         }
//
//         try
//         {
//             await using var fileStream = File.OpenRead(fileName);
//             using BinaryReader binaryReader = new(fileStream);
//             
//             var torrentData = binaryReader.ReadBytes((int)fileStream.Length);
//             
//             var encodeValue = Encoding.ASCII.GetString(torrentData);
//             var decodedValue = BEncoding.Decode(encodeValue);
//             var serializedValue = JsonSerializer.Serialize(decodedValue);
//             var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//             var torrentFile = JsonSerializer.Deserialize<TorrentFile>(serializedValue, jsonSerializerOptions)!;
//             const string infoMarker = "4:infod";
//             var hashStart = encodeValue.IndexOf(infoMarker, StringComparison.Ordinal) +
//                 infoMarker.Length - 1;
//             InfoHash = torrentData[hashStart..^ 1];
//             Announce = torrentFile.Announce;
//             PieceLength = torrentFile.Info.PieceLength;
//             Length = torrentFile.Info.Length;
//             Name = torrentFile.Info.Name;
//             
//             const string piecesMark = "6:pieces";
//             var piecesBytesStart = torrentData[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
//             var piecesStreamStart = encodeValue[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
//             var piecesChunk = piecesBytesStart[(piecesStreamStart.IndexOf(':', StringComparison.Ordinal) + 1)..^1];
//             piecesChunk.Chunk(20).ToList().ForEach(
//                 x => PieceHashes.Add(x));
//             
//         }
//         catch (Exception e)
//         {
//             Console.WriteLine("Error parsing torrent file: " + e.Message);
//             throw;
//         }
//     }
// }
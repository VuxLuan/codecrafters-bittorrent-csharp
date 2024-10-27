using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace codecrafters_bittorrent;

public static class TorrentFileParser
{
    private record TorrentFile(string Announce, TorrentFileInfo Info);

    private record TorrentFileInfo(
        long Length, 
        string Name, 
        [property: JsonPropertyName("piece length")] int PieceLength, 
        string Pieces
    );

    public static void Parser(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException("File not found", fileName);
        }

        try
        {
            var torrentData = File.ReadAllBytes(fileName);
            var encodeValue = Encoding.ASCII.GetString(torrentData);
            var decodedValue = BEncoding.Decode(encodeValue);
            var serializedValue = JsonSerializer.Serialize(decodedValue);
            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var torrentFile = JsonSerializer.Deserialize<TorrentFile>(serializedValue, jsonSerializerOptions)!;
            const string infoMarker = "4:infod";
            var hashStart = encodeValue.IndexOf(infoMarker, StringComparison.Ordinal) +
                infoMarker.Length - 1;
            var chunk = torrentData[hashStart..^ 1];
            var hash = Convert.ToHexString(SHA1.HashData(chunk)).ToLower();
            Console.WriteLine(
                $"Tracker URL: {torrentFile.Announce} \nLength: {torrentFile.Info.Length} \nInfo Hash: {hash} \nPiece Length: {torrentFile.Info.PieceLength}");
            
            // Encoding.ASCII.GetBytes(torrentFile.Info.Pieces).Chunk(20).ToList().ForEach(
            //     x => Console.WriteLine(Convert.ToHexString(x).ToLower()));
            const string piecesMark = "6:pieces";
            var piecesBytesStart = torrentData[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
            var piecesStreamStart = encodeValue[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
            var pchunk = piecesBytesStart[(piecesStreamStart.IndexOf(':', StringComparison.Ordinal) + 1)..^1];
            pchunk.Chunk(20).ToList().ForEach(
                x => Console.WriteLine(Convert.ToHexString(x).ToLower()));
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing torrent file: " + e.Message);
        }
    }
}
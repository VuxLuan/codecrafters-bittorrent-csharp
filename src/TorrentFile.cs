using System.Net;
using System.Text.Json.Serialization;

namespace codecrafters_bittorrent;

public record TorrentFile(string Announce, TorrentFileInfo Info);

public record TorrentFileInfo(
    long Length, 
    string Name, 
    [property: JsonPropertyName("piece length")] 
    int PieceLength, 
    string Pieces
);

public record PeerEndpoint(IPAddress Ip, int Port);


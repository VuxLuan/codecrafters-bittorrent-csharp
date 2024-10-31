using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace codecrafters_bittorrent;

public class Tracker(string fileName)
{
    [StringLength(20, MinimumLength = 20, ErrorMessage = "The string must be exactly 20 characters long.")]
    public string PeerId { get; set; } = GeneratePeerId();
    private int Port { get; set; } = 6881;
    private int Uploaded { get; set; } = 0;
    private int Downloaded { get; set; } = 0;
    private int Compact { get; set; } = 1;
    
    public byte[]? InfoHash { get; set; }
    
    public string FileName { get; set; } = fileName;

    private TorrentFileParser FileParser { get; set; } = new();
    private static readonly HttpClient HttpClient = new();

    private static string GeneratePeerId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 20);
    }

    private async Task<byte[]> BuildTrackerGetRequestAsync()
    {
        await FileParser.ParserAsync(FileName);
        InfoHash = SHA1.HashData(FileParser.InfoHash!);
        var infoHashEncoded = HttpUtility.UrlEncode(InfoHash);
        
        // Build the URI with query parameters
        var uriBuilder = new UriBuilder(FileParser.Announce);
     
        var query = $"info_hash={infoHashEncoded}&peer_id={PeerId}&port={Port}&uploaded={Uploaded}&downloaded={Downloaded}&left={FileParser.Length}&compact={Compact}";
        uriBuilder.Query = query;
        
        var response = await HttpClient.GetAsync(uriBuilder.Uri);
        response.EnsureSuccessStatusCode();
        var contentBytes = await response.Content.ReadAsByteArrayAsync();
        return contentBytes;
    }

    public async Task<List<PeerEndpoint>> GetPeersAsync()
    {
        var content = await BuildTrackerGetRequestAsync();
        var encodeValue = Encoding.ASCII.GetString(content);
        const string piecesMark = "5:peers";
        var piecesBytesStart = content[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
        var piecesStreamStart = encodeValue[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
        var peersIpBytes =  piecesBytesStart[(piecesStreamStart.IndexOf(':', StringComparison.Ordinal) + 1)..^1];

        return (from peer in peersIpBytes.Chunk(6) 
            let ipBytes = peer.Take(4).ToArray() 
            let ipAddress = new IPAddress(ipBytes) 
            let port = peer.Skip(4).Take(2).Aggregate(0, (acc, portByte) => (acc << 8) | portByte) 
            select new PeerEndpoint(ipAddress, port)).ToList();
        
    }


}
    
    


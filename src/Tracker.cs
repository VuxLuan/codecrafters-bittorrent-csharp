using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace codecrafters_bittorrent;

public class Tracker
{
    [StringLength(20, MinimumLength = 20, ErrorMessage = "The string must be exactly 20 characters long.")]
    private string PeerId { get; set; } = GeneratePeerId();
    private int Port { get; set; } = 6881;
    private int Uploaded { get; set; } = 0;
    private int Downloaded { get; set; } = 0;
    private int Compact { get; set; } = 1;

    private TorrentFileParser FileParser { get; set; } = new();
    private readonly HttpClient _httpClient = new();

    private static string GeneratePeerId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 20);
    }

    public async Task<byte[]> BuildTrackerGetRequestAsync(string file)
    {
        await FileParser.ParserAsync(file);




        byte[] infoHash = SHA1.HashData(FileParser.InfoHash!);
        var infoHashEncoded = HttpUtility.UrlEncode(infoHash);
        

        // Build the URI with query parameters
        var uriBuilder = new UriBuilder(FileParser.Announce);
     
        var query = $"info_hash={infoHashEncoded}&peer_id={PeerId}&port={Port}&uploaded={Uploaded}&downloaded={Downloaded}&left={FileParser.Length}&compact={Compact}";
        uriBuilder.Query = query;
        
        var response = await _httpClient.GetAsync(uriBuilder.Uri);
        response.EnsureSuccessStatusCode();
        var contentBytes = await response.Content.ReadAsByteArrayAsync();
        return contentBytes;
        
    }

    public async Task<byte[]> GetPeersBytesAsync(string file)
    {
        
        var content = await BuildTrackerGetRequestAsync(file);
        var encodeValue = Encoding.ASCII.GetString(content);
        const string piecesMark = "5:peers";
        var piecesBytesStart = content[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
        var piecesStreamStart = encodeValue[(encodeValue.IndexOf(piecesMark, StringComparison.Ordinal) + piecesMark.Length - 1)..];
        return  piecesBytesStart[(piecesStreamStart.IndexOf(':', StringComparison.Ordinal) + 1)..^1];
        
        
        
        
    }


}
    
    


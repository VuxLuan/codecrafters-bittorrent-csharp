using System.Security.Cryptography;
using System.Text.Json;
using codecrafters_bittorrent;


async Task ShowInfo(string input)
{
    var fileParser = new TorrentFileParser();
    await fileParser.ParserAsync(input);
    Console.WriteLine(
        $"Tracker URL: {fileParser.Announce} " +
        $"\nLength: {fileParser.Length} " +
        $"\nInfo Hash: {Convert.ToHexString(SHA1.HashData(fileParser.InfoHash!)).ToLower()} " +
        $"\nPiece Length: {fileParser.PieceLength}");
    fileParser.PieceHashes.ForEach(
        x => Console.WriteLine(Convert.ToHexString(x).ToLower()));
}
// Parse arguments
var (command, param) = args.Length switch
{
    0 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    _ => (args[0], args[1])
};


switch (command)
{
    // Parse command, and act accordingly
    case "decode":
        // You can use print statements as follows for debugging, they'll be visible when running tests.
        // Console.WriteLine("Logs from your program will appear here!");
        
        Console.WriteLine(JsonSerializer.Serialize(BEncoding.Decode(param)));
        break;
    
    case "info":
        await ShowInfo(param);
        break;
    case "peers":
        var tracker = new Tracker();
        var peersIpbytes = await tracker.GetPeersBytesAsync(param);
        Console.WriteLine($"PEERS LIST:");
        foreach (var peer in peersIpbytes.Chunk(6)) {
            var peerIp = String.Join(".", peer.Take(4).Select(z => $"{z:d}"));
            var peerPort = peer.Skip(4).Take(2).Aggregate(
                0, (acc, portByte) => (acc << 8) | portByte);
            var p = $"{peerIp}:{peerPort}";
            Console.WriteLine(p);
        }
        break;
    default:
        throw new InvalidOperationException($"Invalid command: {command}");
}


using System.Text;
using System.Text.Json;
using codecrafters_bittorrent;

// Parse arguments
var (command, param) = args.Length switch
{
    0 => throw new InvalidOperationException(
        "Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException(
        "Usage: your_bittorrent.sh <command> <param>"),
    _ => (args[0], args[1])
};
switch (command)
{
    // Parse command and act accordingly
    case "decode":
    {
        var encodedValue = Encoding.UTF8.GetBytes(param);
        Console.WriteLine(JsonSerializer.Serialize(Bencode.Decode(encodedValue)));
        break;
    }
    case "info":
    {
        var torrent = new TorrentClient(param);
        var metaInfo = await torrent.GetMetaDataAsync();
        Console.WriteLine(
            $"Tracker URL: {metaInfo.TrackerUrl}");
        Console.WriteLine($"Length: {metaInfo.Length}");
        Console.WriteLine(
            $"Info Hash: {Convert.ToHexString(metaInfo.InfoHash).ToLower().Replace("-", "")}");
        Console.WriteLine($"Piece Length: {metaInfo.PiecesLength}");
        foreach (var p in metaInfo.PiecesHashes) Console.WriteLine(p);

        break;
    }
    case "peers":
    {
        var torrent = new TorrentClient(param);
        var peersInfo = await torrent.GetPeersAsync();
        foreach (var p in peersInfo) Console.WriteLine(p);
        break;
    }
    case "handshake":
    {
        var torrent = new TorrentClient(param);
        var (handShake, _) = await torrent.HandShakeAsync(args[2]);
        Console.WriteLine(
            $"Peer ID: {Convert.ToHexString(handShake, 48, 20).ToLower().Replace("-", "")}");
        break;
    }
    case "download_piece":
    {
        var outputFilePath = args[2];
        var torrent = new TorrentClient(args[3]);
        var pieceId = int.Parse(args[4]);
        var peers = await torrent.GetPeersAsync();
        var pieceData = await torrent.DownloadPieceAsync(peers[0], pieceId);
        File.WriteAllBytes(outputFilePath, pieceData);
        Console.WriteLine($"Piece {pieceId} downloaded to {outputFilePath}.");
        break;
    }
    default:
        throw new InvalidOperationException($"Invalid command: {command}");
}
namespace codecrafters_bittorrent;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;

public class TorrentClient(string fileName)
{
    private const int BlockSize = 16384; // Standard BitTorrent block size (16KB)
    private const int HandshakeLength = 68;
    private const int ConnectionTimeout = 10000; // 10 seconds

    private NetworkStream? _stream;
    private TcpClient? _tcpClient;
    

    private async Task<byte[]> ReadFileAsync()
    {
        try
        {
            return await File.ReadAllBytesAsync(fileName);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to read torrent file: {ex.Message}", ex);
        }
    }
    
    private void CleanupConnection()
    {
        if (_stream != null)
        {
            _stream.Dispose();
            _stream = null;
        }

        if (_tcpClient == null) return;
        _tcpClient.Dispose();
        _tcpClient = null;
    }

    public async Task<MetaData> GetMetaDataAsync()
    {
        try
        {
            var listPieceHashes = new List<string>();
            var fileContents = await ReadFileAsync();
            var dict = (BencodedDict)Bencode.Decode(fileContents);
            var infoDict = (BencodedDict)dict.Dict["info"];
            var infoHash = SHA1.HashData(infoDict.OriginalData);
            var pieceHashesBytes = (ArraySegment<byte>)infoDict.Dict["pieces"].Value;

            for (var i = 0; i < pieceHashesBytes.Count; i += 20)
            {
                var pieceHash = pieceHashesBytes.Slice(i, 20);
                listPieceHashes.Add(Convert.ToHexString(pieceHash).ToLower().Replace("-", ""));
            }

            return new MetaData(
                TrackerUrl: $"{((BencodedString)dict.Dict["announce"]).Str}",
                Length: (long)infoDict.Dict["length"].Value,
                InfoHash: infoHash,
                PiecesLength: (long)infoDict.Dict["piece length"].Value,
                PiecesHashes: listPieceHashes
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse torrent metadata: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetPeersAsync()
    {
        var listPeers = new List<string>();
        var metaInfo = await GetMetaDataAsync();

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            var uriBuilder = new UriBuilder(metaInfo.TrackerUrl)
            {
                Query = $"?info_hash={HttpUtility.UrlEncode(metaInfo.InfoHash)}" +
                        $"&peer_id={GeneratePeerId()}" +
                        $"&port=6881&uploaded=0&downloaded=0&left={metaInfo.Length}&compact=1"
            };

            var encodedResponse = await httpClient.GetByteArrayAsync(uriBuilder.Uri);
            var response = (BencodedDict)Bencode.Decode(encodedResponse);
            var peers = (ArraySegment<byte>)response.Dict["peers"].Value;

            for (var i = 0; i < peers.Count; i += 6)
            {
                var peerIpBytes = peers.Slice(i, 4);
                var peerPort = BitConverter.ToUInt16(peers.Slice(i + 4, 2));
                if (BitConverter.IsLittleEndian)
                {
                    peerPort = BinaryPrimitives.ReverseEndianness(peerPort);
                }

                listPeers.Add($"{new IPAddress(peerIpBytes)}:{peerPort}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve peers: {ex.Message}", ex);
        }

        return listPeers;
    }

    public async Task<(byte[] HandshakeResponse, NetworkStream Stream)> HandShakeAsync(string peerIp)
    {
        // Cleanup any existing connection before creating a new one
        CleanupConnection();

        var fileContents = await ReadFileAsync();
        var dict = (BencodedDict)Bencode.Decode(fileContents);
        var infoDict = (BencodedDict)dict.Dict["info"];
        var peerEndpoint = IPEndPoint.Parse(peerIp);

        _tcpClient = new TcpClient();
        var connectTask = _tcpClient.ConnectAsync(peerEndpoint);

        if (await Task.WhenAny(connectTask, Task.Delay(ConnectionTimeout)) != connectTask)
        {
            CleanupConnection();
            throw new TimeoutException($"Connection to peer {peerIp} timed out");
        }

        await connectTask;
        _stream = _tcpClient.GetStream();
        _stream.ReadTimeout = ConnectionTimeout;
        _stream.WriteTimeout = ConnectionTimeout;

        var buffer = new byte[HandshakeLength];
        buffer[0] = 19;
        Encoding.UTF8.GetBytes("BitTorrent protocol", buffer.AsSpan()[1..20]);
        SHA1.HashData(infoDict.OriginalData, buffer.AsSpan()[28..48]);
        Encoding.UTF8.GetBytes(GeneratePeerId(), buffer.AsSpan()[48..]);

        await _stream.WriteAsync(buffer);
        await _stream.FlushAsync();

        var response = new byte[HandshakeLength];
        var bytesRead = await _stream.ReadAsync(response.AsMemory(0, HandshakeLength));

        if (bytesRead == HandshakeLength) return (response, _stream);
        CleanupConnection();
        throw new InvalidOperationException($"Invalid handshake response length: {bytesRead}");

    }

    public async Task<byte[]> DownloadPieceAsync(string peerIp, int pieceIndex)
    {
        try
        {
            await HandShakeAsync(peerIp);
            if (_stream == null)
            {
                throw new InvalidOperationException("Handshake must be performed before downloading a piece");
            }

            ReceiveMessage(MessageType.Bitfield);
            await SendMessageAsync(MessageType.Interested);
            ReceiveMessage(MessageType.Unchoke);

            var metaInfo = await GetMetaDataAsync();
            var pieceLength = GetPieceLength(metaInfo, pieceIndex);
            List<byte> pieceData = [];

            for (var i = 0; i < Math.Ceiling((double)pieceLength / BlockSize); i++)
            {
                var blockOffset = i * BlockSize;
                var blockSize = Math.Min(BlockSize, pieceLength - blockOffset);
                await RequestBlockAsync(pieceIndex, blockOffset, blockSize);
                var data = ReceiveMessage(MessageType.Piece);
                pieceData.AddRange(data[8..]);
            }

            if (!VerifyPieceIntegrity(pieceData.ToArray(), metaInfo.PiecesHashes[pieceIndex]))
            {
                throw new InvalidOperationException($"Invalid piece hash: {metaInfo.PiecesHashes[pieceIndex]}");
            }

            return pieceData.ToArray();
        }
        finally
        {
            CleanupConnection();
        }
    }

    public async Task<byte[]> DownloadFileAsync()
    {
        var metaInfo = await GetMetaDataAsync();
        var peers = await GetPeersAsync();
        var pieceCount = metaInfo.PiecesHashes.Count;
        var fileData = new byte[metaInfo.Length];
        var offset = 0;

        for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
        {
            var pieceData = await DownloadPieceAsync(peers[0], pieceIndex);
            pieceData.CopyTo(fileData, offset);
            offset += pieceData.Length;
        }

        return fileData;
    }


    private static bool VerifyPieceIntegrity(byte[] pieceBytes,
        string originalHash)
    {
        return Convert.ToHexString(SHA1.HashData(pieceBytes))
            .Equals(originalHash, StringComparison.CurrentCultureIgnoreCase);
    }

    private async Task SendMessageAsync(MessageType type, byte[]? payload = null)
    {
        var payloadLength = payload?.Length ?? 0;
        var messageLength = 1 + payloadLength;

        var message = new byte [4 + messageLength];

        BitConverter.GetBytes(messageLength).Reverse().ToArray().CopyTo(message, 0);
        message[4] = (byte)type;

        // Write the payload, if any
        if (payload != null)
        {
            Array.Copy(payload, 0, message, 5, payloadLength);
        }

        // Send the message asynchronously
        await _stream!.WriteAsync(message);
    }


    private byte[] ReceiveMessage(MessageType messageType)
    {
        var messageLengthBytes = new byte[4];
        _stream!.ReadExactly(messageLengthBytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(messageLengthBytes);
        }

        var messageLength = BitConverter.ToInt32(messageLengthBytes.ToArray(), 0);

        var messageTypeBytes = (byte)_stream.ReadByte();

        if (messageTypeBytes != (byte)messageType)
        {
            throw new Exception($"Wrong message type: {messageType}. Instead received {messageTypeBytes}");
        }

        var payload = new byte[messageLength - 1];
        _stream.ReadExactly(payload, 0, payload.Length);
        return payload;
    }


    private async Task RequestBlockAsync(int index, int begin, int length)
    {
        var payload = new byte[12];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan()[..4], index);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan()[4..8], begin);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan()[8..12], length);
        //Console.WriteLine($"Requesting block: Index={index}, Begin={begin}, Length={length}");
        await SendMessageAsync(MessageType.Request, payload);
    }


    private static int GetPieceLength(MetaData metaInfo, int pieceIndex)
    {
        var remainingBytes = (int)(metaInfo.Length - pieceIndex * metaInfo.PiecesLength);
        return Math.Min((int)metaInfo.PiecesLength, remainingBytes);
    }

    private static string GeneratePeerId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 20)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }

    public record struct MetaData(
        string TrackerUrl,
        long Length,
        byte[] InfoHash,
        long PiecesLength,
        List<string> PiecesHashes);

    private enum MessageType : byte
    {
        Unchoke = 1,
        Interested = 2,
        Bitfield = 5,
        Request = 6,
        Piece = 7
    }
}
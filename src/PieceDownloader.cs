using System.Buffers.Binary;
using System.Security.Cryptography;

namespace codecrafters_bittorrent;

// Breaks pieces into 16KiB blocks
// Handles request/response for each block
// Combines blocks into complete pieces 
// Verifies piece integrity using SHA1 hash

public class PieceDownloader
{
    private const int BlockSize = 16 * 1024; // 16 KiB
    private readonly Stream _stream;
    private readonly int _pieceIndex;
    private readonly int _pieceLength;
    private readonly byte[] _expectedHash;

    public PieceDownloader(Stream stream, int pieceIndex, int pieceLength, byte[] expectedHash)
    {
        _stream = stream;
        _pieceIndex = pieceIndex;
        _pieceLength = pieceLength;
        _expectedHash = expectedHash;
    }

    public async Task<byte[]> DownloadPieceAsync()
    {
        // Wait for bitfield
        var bitfieldMessage = await PeerMessage.ReadFromStreamAsync(_stream);
        if (bitfieldMessage.Id != MessageId.Bitfield)
            throw new Exception("Expected bitfield message");

        // Send interested
        var interested = PeerMessage.CreateInterested();
        await _stream.WriteAsync(interested.Serialize());

        // Wait for unchoke
        var unchokeMessage = await PeerMessage.ReadFromStreamAsync(_stream);
        if (unchokeMessage.Id != MessageId.Unchoke)
            throw new Exception("Expected unchoke message");

        // Calculate number of blocks needed
        var numBlocks = (_pieceLength + BlockSize - 1) / BlockSize;
        var pieceData = new byte[_pieceLength];
        var receivedBlocks = 0;

        // Request all blocks
        for (var i = 0; i < numBlocks; i++)
        {
            var begin = i * BlockSize;
            var blockLength = Math.Min(BlockSize, _pieceLength - begin);
            
            var request = PeerMessage.CreateRequest(_pieceIndex, begin, blockLength);
            await _stream.WriteAsync(request.Serialize());
        }

        // Receive all blocks
        while (receivedBlocks < numBlocks)
        {
            var pieceMessage = await PeerMessage.ReadFromStreamAsync(_stream);
            if (pieceMessage.Id != MessageId.Piece)
                throw new Exception("Expected piece message");

            var index = BinaryPrimitives.ReadInt32BigEndian(pieceMessage.Payload.AsSpan(0, 4));
            var begin = BinaryPrimitives.ReadInt32BigEndian(pieceMessage.Payload.AsSpan(4, 4));
            var blockData = pieceMessage.Payload[8..];

            Buffer.BlockCopy(blockData, 0, pieceData, begin, blockData.Length);
            receivedBlocks++;
        }

        // Verify piece hash
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(pieceData);
        if (!hash.SequenceEqual(_expectedHash))
            throw new Exception("Piece hash verification failed");

        return pieceData;
    }
}
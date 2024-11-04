using System.Buffers.Binary;

namespace codecrafters_bittorrent;


public enum MessageId : byte
{
    Choke = 0,
    Unchoke = 1,
    Interested = 2,
    NotInterested = 3,
    Have = 4,
    Bitfield = 5,
    Request = 6,
    Piece = 7,
    Cancel = 8
    
}

// Properly formats message length and IDs
// Handles various message types (bitfield, interested, unchoke, request, piece)
// Proper serialization/deserialization of messages

public  class PeerMessage
{
    public int Length { get; }
    public MessageId? Id { get; }
    public byte[] Payload { get; }

    public PeerMessage(MessageId id, byte[] payload)
    {
        Id = id;
        Payload = payload;
        Length = payload.Length + 1; 
    }
    
    public PeerMessage()
    {
        Id = null;
        Payload = [];
        Length = 0;
    }
    
    public byte[] Serialize()
    {
        var result = new byte[4 + (Id.HasValue ? 1 + Payload.Length : 0)];
        BinaryPrimitives.WriteInt32BigEndian(result, Length);

        if (!Id.HasValue) return result;
        result[4] = (byte)Id.Value;
        Payload.CopyTo(result, 5);

        return result;
    }
    
    public static async Task<PeerMessage> ReadFromStreamAsync(Stream stream)
    {
        var lengthBytes = new byte[4];
        await stream.ReadAsync(lengthBytes);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

        if (length == 0)
            return new PeerMessage();

        var message = new byte[length];
        await stream.ReadAsync(message);

        var id = (MessageId)message[0];
        var payload = message[1..];

        return new PeerMessage(id, payload);
    }

    // Helper method to create an Interested message
    public static PeerMessage CreateInterested()
        => new(MessageId.Interested, Array.Empty<byte>());

    // Helper method to create a Request message
    public static PeerMessage CreateRequest(int index, int begin, int length)
    {
        var payload = new byte[12];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), index);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4, 4), begin);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(8, 4), length);
        return new PeerMessage(MessageId.Request, payload);
    }
    
}
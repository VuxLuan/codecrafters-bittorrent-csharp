// using System.Net.Sockets;
// using System.Text;
//
// namespace codecrafters_bittorrent;
//
// public class HandShake (string fileName)
// {
//     public int Length { get; set; } = 19;
//     private string Pstr { get; set; } = "BitTorrent protocol";
//     private const int ReservedBytesLength = 8;
//     private const int HashSize = 20;
//     private Tracker Tracker { get; set; } = new(fileName);
//     public string FileName { get; set; } = fileName;
//
//     
//     public  async Task TcpHandShake(PeerEndpoint peerEndpoint)
//     {
//         
//         
//         
//             try
//             {
//                 using var client = new TcpClient();
//                 await client.ConnectAsync(peerEndpoint.Ip, peerEndpoint.Port);
//                 Console.WriteLine("Connected to the server.");
//                 await using var stream = client.GetStream();
//                 
//
//                 await stream.WriteAsync(SerializeMessage());
//                 Console.WriteLine(Encoding.ASCII.GetString(SerializeMessage()).Length);
//                 Console.WriteLine("Sending message...");
//
//                 // Buffer for receiving data
//                 byte[] buffer = new byte[256];
//
//                 // Read the server response
//                 var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
//                 var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
//
//                 Console.WriteLine($"Received: {response}");
//             }
//             catch (Exception e)
//             {
//                 Console.WriteLine(e);
//                 throw;
//             }
//         }
//     
//     private byte[] SerializeMessage()
//     {
//
//         // Calculate the length of the buffer: length of Pstr + 49 bytes (1 + 8 + 20 + 20)
//         byte[] buf = new byte[Pstr.Length + 49];
//
//         // Set the first byte to the length of Pstr
//         buf[0] = (byte)Pstr.Length;
//         int curr = 1;
//
//         // Copy Pstr into buf starting at index 1
//         byte[] pstrBytes = Encoding.ASCII.GetBytes(Pstr);
//         Array.Copy(pstrBytes, 0, buf, curr, pstrBytes.Length);
//         curr += pstrBytes.Length;
//
//         // Initialize reserved bytes (explicitly setting to zero, although it's not necessary)
//         curr += ReservedBytesLength; // Reserved bytes are already zero-initialized
//
//         // Copy InfoHash (20 bytes)
//         if (Tracker.InfoHash!.Length != HashSize)
//         {
//             throw new InvalidOperationException("InfoHash must be exactly 20 bytes.");
//         }
//         Array.Copy(Tracker.InfoHash, 0, buf, curr, HashSize);
//         curr += HashSize;
//
//         // Copy PeerID (20 bytes)
//         if (Tracker.PeerId.Length != HashSize)
//         {
//             throw new InvalidOperationException("PeerID must be exactly 20 bytes.");
//         }
//         Array.Copy(Encoding.ASCII.GetBytes(Tracker.PeerId), 0, buf, curr, HashSize);
//         curr += HashSize;
//
//         return buf;
//     }
//     
//     public string DeserializeMessage(byte[] message)
//     {
//         var index = message;
//     }
//     }
//     
// }
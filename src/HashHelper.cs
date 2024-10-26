// using System.Security.Cryptography;
// using System.Text;
//
// namespace codecrafters_bittorrent;
//
// public static class HashHelper
// {
//     public static string ComputeSha1Hash(byte[] input)
//     {
//         using SHA1 sha1 = SHA1.Create();
//         byte[] data = sha1.ComputeHash(input);
//
//         // Convert byte array to a hexadecimal string
//         StringBuilder hashBuilder = new StringBuilder();
//         foreach (byte b in data)
//         {
//             hashBuilder.Append(b.ToString("x2"));
//         }
//         return hashBuilder.ToString();
//     }
// }
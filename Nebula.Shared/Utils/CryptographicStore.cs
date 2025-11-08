using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace Nebula.Shared.Utils;

public static class CryptographicStore
{
   public static string Encrypt(object value, byte[] key)
   {
      using var memoryStream = new MemoryStream();
      using var aes = Aes.Create();
      aes.Key = key;

      var iv = aes.IV;
      memoryStream.Write(iv, 0, iv.Length);
      
      var serializedData = JsonSerializer.Serialize(value);

      using CryptoStream cryptoStream = new(
         memoryStream,
         aes.CreateEncryptor(),
         CryptoStreamMode.Write);
      
      using(StreamWriter encryptWriter = new(cryptoStream))
      {
         encryptWriter.WriteLine(serializedData);
      }
      
      return Convert.ToBase64String(memoryStream.ToArray());
   }

   public static async Task<T> Decrypt<T>(string base64EncryptedValue, byte[] key)
   {
      using var memoryStream = new MemoryStream(Convert.FromBase64String(base64EncryptedValue));
      using var aes = Aes.Create();
      
      var iv = new byte[aes.IV.Length];
      var numBytesToRead = aes.IV.Length;
      var numBytesRead = 0;
      while (numBytesToRead > 0)
      {
         var n = memoryStream.Read(iv, numBytesRead, numBytesToRead);
         if (n == 0) break;

         numBytesRead += n;
         numBytesToRead -= n;
      }


      await using CryptoStream cryptoStream = new(
         memoryStream,
         aes.CreateDecryptor(key, iv),
         CryptoStreamMode.Read);

      using StreamReader decryptReader = new(cryptoStream);
      var decryptedMessage = await decryptReader.ReadToEndAsync();
      return JsonSerializer.Deserialize<T>(decryptedMessage) ?? throw new InvalidOperationException();
   }

   public static byte[] GetKey(string input, int keySize = 256)
   {
      if (string.IsNullOrEmpty(input))
         throw new ArgumentException("Input string cannot be null or empty.", nameof(input));

      var salt = Encoding.UTF8.GetBytes(input);
      
      using (var deriveBytes = new Rfc2898DeriveBytes(input, salt, 100_000, HashAlgorithmName.SHA256))
      {
         return deriveBytes.GetBytes(keySize / 8);
      }
   }

   public static byte[] GetComputerKey(int keySize = 256)
   {
      var name = Environment.UserName;
      if (string.IsNullOrEmpty(name))
         name = "LinuxUser";
      return GetKey(name, keySize);
   }
}
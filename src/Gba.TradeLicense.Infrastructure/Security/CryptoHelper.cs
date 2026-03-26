using System.Security.Cryptography;
using System.Text;

namespace Gba.TradeLicense.Infrastructure.Security
{
    public static class CryptoHelper
    {
        // 🔐 AES ENCRYPT
        public static string EncryptAES(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(cipherBytes);
        }

        // 🔓 AES DECRYPT
        public static string DecryptAES(string encryptedText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        // 🔐 RSA ENCRYPT (PUBLIC KEY)
        public static string EncryptRSA(byte[] data, string publicKeyPem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem.ToCharArray());

            var encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(encrypted);
        }

        // 🔓 RSA DECRYPT (PRIVATE KEY)
        public static byte[] DecryptRSA(string encryptedData, string privateKeyPem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem.ToCharArray());

            return rsa.Decrypt(Convert.FromBase64String(encryptedData), RSAEncryptionPadding.OaepSHA256);
        }

        // 📂 READ PUBLIC KEY
        public static string GetPublicKey()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Security", "Keys", "public_key.pem");
            return File.ReadAllText(path);
        }

        // 📂 READ PRIVATE KEY
        public static string GetPrivateKey()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Security", "Keys", "private_key.pem");
            return File.ReadAllText(path);
        }
    }
}
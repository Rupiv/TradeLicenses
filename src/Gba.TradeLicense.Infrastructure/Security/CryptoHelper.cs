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

        // Key pair for the payment-success redirect (this API encrypts the payload, the same API
        // decrypts it a few seconds later; it never leaves the server). Supplied by configuration
        // (Program.cs -> Configure), never from source control:
        //   PaymentRedirect:PublicKeyPem / PrivateKeyPem   (PEM text), or
        //   PaymentRedirect:PublicKeyPath / PrivateKeyPath (PEM files outside the web root)
        // If neither is configured, the files an older deployment left in <app>\Security\Keys are
        // used; failing that, a fresh in-memory key pair is generated at start-up (fine for one
        // server: a redirect is decrypted seconds after it is made).
        private static string? _publicKeyPem;
        private static string? _privateKeyPem;
        private static readonly object KeySync = new();

        /// <summary>Returns a short description of where the keys came from (no key material).</summary>
        public static string Configure(string? publicKeyPem, string? privateKeyPem, string? publicKeyPath, string? privateKeyPath)
        {
            lock (KeySync)
            {
                if (!string.IsNullOrWhiteSpace(publicKeyPem) && !string.IsNullOrWhiteSpace(privateKeyPem))
                {
                    _publicKeyPem = publicKeyPem; _privateKeyPem = privateKeyPem;
                    return "configuration (PEM)";
                }
                if (!string.IsNullOrWhiteSpace(publicKeyPath) && !string.IsNullOrWhiteSpace(privateKeyPath))
                {
                    _publicKeyPem = File.ReadAllText(publicKeyPath); _privateKeyPem = File.ReadAllText(privateKeyPath);
                    return "configuration (key files)";
                }

                var legacyPublic = Path.Combine(AppContext.BaseDirectory, "Security", "Keys", "public_key.pem");
                var legacyPrivate = Path.Combine(AppContext.BaseDirectory, "Security", "Keys", "private_key.pem");
                if (File.Exists(legacyPublic) && File.Exists(legacyPrivate))
                {
                    _publicKeyPem = File.ReadAllText(legacyPublic); _privateKeyPem = File.ReadAllText(legacyPrivate);
                    return "legacy Security/Keys files (rotate: this key was committed to source control)";
                }

                using var rsa = RSA.Create(2048);
                _publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
                _privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
                return "generated in memory at start-up";
            }
        }

        // 📂 PUBLIC KEY
        public static string GetPublicKey()
        {
            if (_publicKeyPem == null) Configure(null, null, null, null);
            return _publicKeyPem!;
        }

        // 📂 PRIVATE KEY
        public static string GetPrivateKey()
        {
            if (_privateKeyPem == null) Configure(null, null, null, null);
            return _privateKeyPem!;
        }
    }
}
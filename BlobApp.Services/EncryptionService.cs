using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using BlobApp.Services.Interfaces;

namespace BlobApp.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _encryptionKey;

        public EncryptionService(IConfiguration configuration)
        {
            var key = configuration["EncryptionSettings:Key"];
            _encryptionKey = Convert.FromBase64String(ConvertToBase64(key));
        }

        private static string ConvertToBase64(string key)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
            return Convert.ToBase64String(keyBytes);
        }

        public byte[] EncryptData(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.GenerateIV();
                var iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        public byte[] DecryptData(byte[] encryptedData)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;

                using (var ms = new MemoryStream(encryptedData))
                {
                    var iv = new byte[aes.BlockSize / 8];
                    ms.Read(iv, 0, iv.Length);
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var resultStream = new MemoryStream())
                    {
                        cs.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
        }
    }
}

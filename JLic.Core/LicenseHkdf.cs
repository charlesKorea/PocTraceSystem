using System;
using System.Security.Cryptography;
using System.Text;

namespace JLic
{
    internal static class LicenseHkdf
    {
        public const string AesInfo = "JLIC-AES-v1";
        public const string MacInfo = "JLIC-MAC-v1";

        public static byte[] Derive(byte[] ikm, byte[] salt, string info, int length)
        {
            using (var extract = new HMACSHA256(salt))
            {
                byte[] prk = extract.ComputeHash(ikm);
                using (var expand = new HMACSHA256(prk))
                {
                    byte[] infoBytes = Encoding.UTF8.GetBytes(info);
                    var input = new byte[infoBytes.Length + 1];
                    Buffer.BlockCopy(infoBytes, 0, input, 0, infoBytes.Length);
                    input[infoBytes.Length] = 1;
                    byte[] okm = expand.ComputeHash(input);
                    if (okm.Length == length)
                        return okm;
                    var cut = new byte[length];
                    Buffer.BlockCopy(okm, 0, cut, 0, length);
                    return cut;
                }
            }
        }
    }
}

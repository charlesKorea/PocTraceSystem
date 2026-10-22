using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JLic
{
    public sealed class SealedLicense
    {
        public byte[] UnsignedFile { get; set; }
        public byte[] SignInput { get { return UnsignedFile; } }
    }

    public static class LicenseContainer
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("JLIC");
        public const byte Version = 1;
        public const int HeaderLength = 42;
        public const int HmacLength = 32;
        public const int RsaSignatureLength = 256;

        public static SealedLicense Seal(LicenseDocument document, LicenseClientKeys keys)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (keys == null)
                throw new ArgumentNullException("keys");

            byte[] plaintext = Encoding.UTF8.GetBytes(LicenseJson.Write(document));
            byte[] salt = RandomBytes(16);
            byte[] iv = RandomBytes(16);
            byte[] aesKey = LicenseHkdf.Derive(keys.AesKey, salt, LicenseHkdf.AesInfo, 32);
            byte[] hmacKey = LicenseHkdf.Derive(keys.HmacPepper, salt, LicenseHkdf.MacInfo, 32);
            byte[] ciphertext = AesCbc(aesKey, iv, plaintext, true);
            byte[] header = LicenseBytes.Concat(
                Magic,
                new[] { Version, (byte)0 },
                salt,
                iv,
                LicenseBytes.Le32(ciphertext.Length));
            byte[] body = LicenseBytes.Concat(header, ciphertext);
            byte[] tag;
            using (var hmac = new HMACSHA256(hmacKey))
                tag = hmac.ComputeHash(body);

            return new SealedLicense
            {
                UnsignedFile = LicenseBytes.Concat(body, tag)
            };
        }

        public static byte[] AttachSignature(byte[] unsignedFile, byte[] signature)
        {
            if (unsignedFile == null)
                throw new ArgumentNullException("unsignedFile");
            if (signature == null || signature.Length != RsaSignatureLength)
                throw new ArgumentException("RSA-2048 서명은 256바이트여야 합니다.");
            return LicenseBytes.Concat(unsignedFile, signature);
        }

        public static LicenseDocument Open(byte[] fileBytes, LicenseClientKeys keys)
        {
            LicenseErrorCode code;
            string error;
            LicenseDocument document = TryOpen(fileBytes, keys, out code, out error);
            if (document == null)
                throw new InvalidDataException(error);
            return document;
        }

        public static LicenseDocument TryOpen(byte[] fileBytes, LicenseClientKeys keys, out LicenseErrorCode code, out string error)
        {
            code = LicenseErrorCode.FileInvalid;
            error = null;
            if (fileBytes == null || fileBytes.Length < HeaderLength + HmacLength + RsaSignatureLength)
            {
                error = "라이선스 파일이 너무 짧거나 손상되었습니다.";
                return null;
            }
            if (fileBytes[0] != Magic[0] || fileBytes[1] != Magic[1] || fileBytes[2] != Magic[2] || fileBytes[3] != Magic[3])
            {
                error = "jlab.lic 매직(JLIC)이 아닙니다. 구형 TripleDES 파일이면 JLic.Core로 다시 발급하세요.";
                return null;
            }
            if (fileBytes[4] != Version)
            {
                error = "지원하지 않는 라이선스 버전입니다: " + fileBytes[4];
                return null;
            }

            byte[] salt = Slice(fileBytes, 6, 16);
            byte[] iv = Slice(fileBytes, 22, 16);
            int cipherLen = LicenseBytes.ReadLe32(fileBytes, 38);
            int need = HeaderLength + cipherLen + HmacLength + RsaSignatureLength;
            if (cipherLen < 16 || fileBytes.Length != need)
            {
                error = "라이선스 길이 정보가 맞지 않습니다.";
                return null;
            }

            byte[] ciphertext = Slice(fileBytes, HeaderLength, cipherLen);
            byte[] tag = Slice(fileBytes, HeaderLength + cipherLen, HmacLength);
            byte[] signature = Slice(fileBytes, fileBytes.Length - RsaSignatureLength, RsaSignatureLength);
            byte[] unsigned = Slice(fileBytes, 0, fileBytes.Length - RsaSignatureLength);
            byte[] signedArea = Slice(unsigned, 0, unsigned.Length - HmacLength);

            if (!VerifyRsa(keys.RsaPublicXml, unsigned, signature))
            {
                code = LicenseErrorCode.SignatureInvalid;
                error = "라이선스 서명이 올바르지 않습니다. 파일이 위조되었거나 키가 다릅니다.";
                return null;
            }

            byte[] hmacKey = LicenseHkdf.Derive(keys.HmacPepper, salt, LicenseHkdf.MacInfo, 32);
            byte[] expected;
            using (var hmac = new HMACSHA256(hmacKey))
                expected = hmac.ComputeHash(signedArea);
            if (!LicenseBytes.FixedEquals(tag, expected))
            {
                code = LicenseErrorCode.IntegrityInvalid;
                error = "라이선스 HMAC 무결성 검사에 실패했습니다.";
                return null;
            }

            try
            {
                byte[] aesKey = LicenseHkdf.Derive(keys.AesKey, salt, LicenseHkdf.AesInfo, 32);
                byte[] plaintext = AesCbc(aesKey, iv, ciphertext, false);
                code = LicenseErrorCode.None;
                return LicenseJson.Read(Encoding.UTF8.GetString(plaintext));
            }
            catch (Exception ex)
            {
                code = LicenseErrorCode.DecryptFailed;
                error = "라이선스 본문을 복호화하지 못했습니다. " + ex.Message;
                return null;
            }
        }

        public static bool VerifyRsa(string publicXml, byte[] data, byte[] signature)
        {
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(publicXml);
                    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] AesCbc(byte[] key, byte[] iv, byte[] data, bool encrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                    return transform.TransformFinalBlock(data, 0, data.Length);
            }
        }

        private static byte[] RandomBytes(int length)
        {
            var data = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(data);
            return data;
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            var copy = new byte[length];
            Buffer.BlockCopy(source, offset, copy, 0, length);
            return copy;
        }
    }
}

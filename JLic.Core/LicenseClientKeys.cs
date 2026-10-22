using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace JLic
{
    public sealed class LicenseClientKeys
    {
        public byte[] AesKey { get; private set; }
        public byte[] HmacPepper { get; private set; }
        public string RsaPublicXml { get; private set; }
        public string SourcePath { get; private set; }

        public static LicenseClientKeys LoadDefault()
        {
            foreach (var path in LicensePaths.ClientKeyCandidates())
            {
                if (File.Exists(path))
                    return LoadFromFile(path);
            }
            return LoadEmbedded();
        }

        public static LicenseClientKeys LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("클라이언트 키 파일을 찾을 수 없습니다.", path);
            var keys = Parse(File.ReadAllText(path));
            keys.SourcePath = path;
            return keys;
        }

        public static LicenseClientKeys LoadEmbedded()
        {
            var assembly = typeof(LicenseClientKeys).Assembly;
            string name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("client.keys", StringComparison.OrdinalIgnoreCase));
            if (name == null)
                throw new InvalidOperationException("임베드된 JLic.client.keys 를 찾지 못했습니다.");

            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                var keys = Parse(reader.ReadToEnd());
                keys.SourcePath = "embedded:" + name;
                return keys;
            }
        }

        public static LicenseClientKeys FromParts(byte[] aesKey, byte[] hmacPepper, string rsaPublicXml)
        {
            if (aesKey == null || aesKey.Length != 32)
                throw new ArgumentException("AES 키는 32바이트여야 합니다.");
            if (hmacPepper == null || hmacPepper.Length != 32)
                throw new ArgumentException("HMAC pepper는 32바이트여야 합니다.");
            if (string.IsNullOrWhiteSpace(rsaPublicXml))
                throw new ArgumentException("RSA 공개키가 비어 있습니다.");
            return new LicenseClientKeys
            {
                AesKey = (byte[])aesKey.Clone(),
                HmacPepper = (byte[])hmacPepper.Clone(),
                RsaPublicXml = rsaPublicXml.Trim()
            };
        }

        public string ToClientXml()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine
                + "<JLicClientKeys version=\"1\">" + Environment.NewLine
                + "  <AesKey>" + Convert.ToBase64String(AesKey) + "</AesKey>" + Environment.NewLine
                + "  <HmacPepper>" + Convert.ToBase64String(HmacPepper) + "</HmacPepper>" + Environment.NewLine
                + "  <RsaPublicXml><![CDATA[" + RsaPublicXml + "]]></RsaPublicXml>" + Environment.NewLine
                + "</JLicClientKeys>" + Environment.NewLine;
        }

        private static LicenseClientKeys Parse(string xml)
        {
            XDocument doc = XDocument.Parse(xml);
            XElement root = doc.Root;
            if (root == null)
                throw new FormatException("키 XML 루트가 없습니다.");

            return FromParts(
                Convert.FromBase64String(Value(root, "AesKey")),
                Convert.FromBase64String(Value(root, "HmacPepper")),
                Value(root, "RsaPublicXml"));
        }

        private static string Value(XElement root, string name)
        {
            XElement el = root.Element(name);
            if (el == null || string.IsNullOrWhiteSpace(el.Value))
                throw new FormatException("키 필드가 없습니다: " + name);
            return el.Value.Trim();
        }
    }
}

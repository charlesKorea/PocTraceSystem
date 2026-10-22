using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JLic
{
    public static class LicenseBinding
    {
        public const string MacDomainPrefix = "MAC|";
        private static readonly Regex MacHex = new Regex("^[0-9A-F]{12}$", RegexOptions.Compiled);

        public static string NormalizeMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return "";

            var chars = mac.ToUpperInvariant().ToCharArray();
            var buffer = new char[chars.Length];
            int n = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
                    buffer[n++] = c;
            }
            return new string(buffer, 0, n);
        }

        public static bool IsNormalizedMac(string mac)
        {
            return !string.IsNullOrEmpty(mac) && MacHex.IsMatch(mac);
        }

        public static bool IsReservedMac(string mac)
        {
            return mac == "000000000000" || mac == "FFFFFFFFFFFF";
        }

        public static string HashMac(byte[] pepper, string mac)
        {
            if (pepper == null)
                throw new ArgumentNullException("pepper");

            string normalized = NormalizeMac(mac);
            using (var hmac = new HMACSHA256(pepper))
            {
                byte[] data = Encoding.UTF8.GetBytes(MacDomainPrefix + normalized);
                return LicenseBytes.ToHex(hmac.ComputeHash(data));
            }
        }

        public static string[] CollectLocalMacs()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => NormalizeMac(nic.GetPhysicalAddress().ToString()))
                    .Where(mac => IsNormalizedMac(mac) && !IsReservedMac(mac))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (NetworkInformationException)
            {
                return new string[0];
            }
        }
    }
}

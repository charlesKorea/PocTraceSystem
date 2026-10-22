using System;

namespace JLic
{
    public sealed class LicenseUserEntry
    {
        public string MacHash { get; set; }
        public string UserName { get; set; }
        public string Group { get; set; }
        public string[] Apps { get; set; }
        public string ExpireDate { get; set; }
    }

    public sealed class LicenseDocument
    {
        public int Format { get; set; }
        public string IssuedAt { get; set; }
        public string Issuer { get; set; }
        public LicenseUserEntry[] Users { get; set; }

        public static LicenseDocument Create(LicenseUserEntry[] users)
        {
            return new LicenseDocument
            {
                Format = 1,
                IssuedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Issuer = "JLicManager",
                Users = users ?? new LicenseUserEntry[0]
            };
        }
    }
}

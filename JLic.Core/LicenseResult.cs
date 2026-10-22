using System;

namespace JLic
{
    public sealed class LicenseResult
    {
        public bool Success { get; internal set; }
        public string UserName { get; internal set; }
        public string Group { get; internal set; }
        public string[] Apps { get; internal set; }
        public DateTime? ExpireDate { get; internal set; }
        public string Error { get; internal set; }
        public LicenseErrorCode ErrorCode { get; internal set; }

        internal static LicenseResult Ok(string userName, string group, string[] apps, DateTime? expire)
        {
            return new LicenseResult
            {
                Success = true,
                UserName = userName ?? "",
                Group = group ?? "",
                Apps = apps ?? new string[0],
                ExpireDate = expire,
                Error = "",
                ErrorCode = LicenseErrorCode.None
            };
        }

        public static LicenseResult Fail(LicenseErrorCode code, string error)
        {
            return new LicenseResult
            {
                Success = false,
                UserName = "",
                Group = "",
                Apps = new string[0],
                ExpireDate = null,
                Error = error ?? "",
                ErrorCode = code
            };
        }

        public bool HasGroup(params string[] groups)
        {
            return LicenseCatalog.HasGroup(Group, groups);
        }

        public bool HasAnyApp(params string[] apps)
        {
            return LicenseCatalog.HasAnyApp(Apps, apps);
        }
    }
}

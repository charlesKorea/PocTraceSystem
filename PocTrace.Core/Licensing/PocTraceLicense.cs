using JLic;

namespace PocTrace.Core.Licensing
{
    /// <summary>
    /// PocTrace Viewer / Web / 철거 / 노즐이 같은 JLic.Core Verify 를 쓴다.
    /// 앱 코드는 PT. 기존 발급 목록의 AL 도 통과시킨다.
    /// </summary>
    public static class PocTraceLicense
    {
        public const string AppCode = "PT";
        public static readonly string[] AcceptedAppCodes = { "PT", "AL" };

        public static LicenseResult Current { get; private set; }

        public static LicenseResult Verify()
        {
            Current = LicenseApi.VerifyAnyApp(AcceptedAppCodes);
            return Current;
        }

        public static LicenseResult VerifyAdmin()
        {
            LicenseResult lic = Verify();
            if (!lic.Success)
                return lic;
            if (!lic.HasGroup("Admin"))
            {
                Current = LicenseResult.Fail(LicenseErrorCode.AppDenied,
                    "PocTrace Admin은 Admin 권한만 사용할 수 있습니다.");
                return Current;
            }
            return lic;
        }

        public static bool IsPartner(LicenseResult lic)
        {
            return lic != null && lic.Success && lic.HasGroup("2차");
        }

        public static bool CanUseExtraTools(LicenseResult lic)
        {
            return lic != null && lic.Success && !IsPartner(lic);
        }

        public static object ClientView(LicenseResult lic)
        {
            if (lic == null || !lic.Success)
            {
                return new
                {
                    ok = false,
                    error = lic == null ? "라이선스가 없습니다." : lic.Error,
                    errorCode = lic == null ? LicenseErrorCode.FileMissing.ToString() : lic.ErrorCode.ToString()
                };
            }

            return new
            {
                ok = true,
                userName = lic.UserName,
                group = lic.Group,
                apps = lic.Apps,
                expireDate = lic.ExpireDate.HasValue ? lic.ExpireDate.Value.ToString("yyyy-MM-dd") : null,
                canDemo = CanUseExtraTools(lic),
                canNozzle = CanUseExtraTools(lic)
            };
        }
    }
}

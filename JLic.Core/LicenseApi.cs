using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JLic
{
    /// <summary>
    /// 각 앱이 호출하는 유일한 검증 API.
    /// 예: var lic = LicenseApi.Verify("BAM");
    /// </summary>
    public static class LicenseApi
    {
        public static LicenseResult Verify(string requiredApp = null, string licensePath = null)
        {
            string[] apps = string.IsNullOrWhiteSpace(requiredApp) ? null : new[] { requiredApp };
            return VerifyAnyApp(apps, licensePath);
        }

        public static LicenseResult VerifyAnyApp(string[] requiredApps, string licensePath = null)
        {
            return VerifyForMacs(LicenseBinding.CollectLocalMacs(), requiredApps, licensePath);
        }

        public static LicenseResult VerifyMachine(string licensePath = null)
        {
            return VerifyForMacs(LicenseBinding.CollectLocalMacs(), null, licensePath);
        }

        public static LicenseResult VerifyForMacs(IEnumerable<string> macs, string[] requiredApps, string licensePath = null)
        {
            LicenseClientKeys keys;
            try
            {
                keys = LicenseClientKeys.LoadDefault();
            }
            catch (Exception ex)
            {
                return LicenseResult.Fail(LicenseErrorCode.KeysMissing, "검증 키를 읽지 못했습니다. " + ex.Message);
            }

            string path = LicensePaths.FindLicenseFile(licensePath);
            if (path == null)
            {
                return LicenseResult.Fail(LicenseErrorCode.FileMissing,
                    "라이선스 파일이 없습니다. 앱 옆 Lic\\jlab.lic 를 배포하세요.");
            }

            byte[] blob;
            try
            {
                blob = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                return LicenseResult.Fail(LicenseErrorCode.FileInvalid, "라이선스 파일을 읽지 못했습니다. " + ex.Message);
            }

            LicenseErrorCode code;
            string error;
            LicenseDocument document = LicenseContainer.TryOpen(blob, keys, out code, out error);
            if (document == null)
                return LicenseResult.Fail(code, error);

            var localMacs = (macs ?? new string[0])
                .Select(LicenseBinding.NormalizeMac)
                .Where(LicenseBinding.IsNormalizedMac)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (localMacs.Length == 0)
            {
                return LicenseResult.Fail(LicenseErrorCode.MachineNotLicensed,
                    "이 PC에서 유효한 물리 MAC을 찾지 못했습니다.");
            }

            var hashes = new HashSet<string>(
                localMacs.Select(mac => LicenseBinding.HashMac(keys.HmacPepper, mac)),
                StringComparer.OrdinalIgnoreCase);

            LicenseUserEntry matched = null;
            if (document.Users != null)
            {
                foreach (var user in document.Users)
                {
                    if (user != null && !string.IsNullOrWhiteSpace(user.MacHash) && hashes.Contains(user.MacHash.Trim()))
                    {
                        matched = user;
                        break;
                    }
                }
            }

            if (matched == null)
            {
                return LicenseResult.Fail(LicenseErrorCode.MachineNotLicensed,
                    "이 PC의 MAC이 라이선스에 없습니다.");
            }

            DateTime? expire = LicenseJson.ParseExpire(matched.ExpireDate);
            if (expire.HasValue && expire.Value.Date < DateTime.Today)
            {
                return LicenseResult.Fail(LicenseErrorCode.Expired,
                    "라이선스가 만료되었습니다: " + expire.Value.ToString("yyyy-MM-dd"));
            }

            var apps = matched.Apps ?? new string[0];
            if (requiredApps != null && requiredApps.Length > 0 && !LicenseCatalog.HasAnyApp(apps, requiredApps))
            {
                return LicenseResult.Fail(LicenseErrorCode.AppDenied,
                    "이 앱(" + string.Join("|", requiredApps) + ")에 대한 권한이 없습니다.");
            }

            return LicenseResult.Ok(matched.UserName, matched.Group, apps, expire);
        }

        public static string[] GetLocalMacAddresses()
        {
            return LicenseBinding.CollectLocalMacs();
        }

        public static string FindLicenseFile(string licensePath = null)
        {
            return LicensePaths.FindLicenseFile(licensePath);
        }
    }
}

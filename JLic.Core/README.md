# JLic.Core

각 프로그램이 라이선스를 검증할 때 쓰는 유일한 DLL입니다. 복호화·HMAC·RSA 서명 확인만 들어 있고, RSA 개인키와 발급 UI는 없습니다.

## 앱에서 호출

```csharp
var lic = JLic.LicenseApi.Verify("BAM"); // AL / JP / SP / BAM / PT
if (!lic.Success)
{
    MessageBox.Show(lic.Error, "라이선스 오류");
    Environment.Exit(0); // 실패 시 폼을 그리지 말고 종료
}

if (!lic.HasGroup("Admin", "SDS", "SCS"))
    return;

bool partner = lic.HasGroup("2차");
bool canPorter = lic.HasAnyApp("JP");
```

앱 코드는 `|`로 나눈 토큰만 비교합니다. `IndexOf("JP")`처럼 부분 문자열을 쓰지 마세요.

## 배포 (앱마다 동일)

꼭 같이 둘 것: `YourApp.exe` + **`JLic.Core.dll`** + **`Lic\jlab.lic`**

두지 말 것: `JLicManager.exe`, `Keys\issuer.keys`

앱별 코드는 `docs/DEPLOY-AND-VERIFY.md` 와 `Integration/LicenseBootstrap.example.cs` 를 보세요.

기본 검증 키는 DLL에 임베드되어 있습니다. 키를 재발급했다면 앱 옆에 `JLic.client.keys`를 같이 두면 임베드 키보다 우선합니다.

## 한계

DLL을 분석하면 AES 키와 HMAC pepper를 꺼낼 수 있어, 파일 **내용**은 복호화될 수 있습니다. 이번 강화의 핵심은 **위조 차단**(RSA 서명)과 **원문 MAC 미저장**(HMAC 바인딩)입니다. 공용 폴더 배포에서 실행 파일 읽기가 가능한 한, 완전한 비밀 유지는 불가능합니다.

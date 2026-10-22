# WinForms .NET Framework 4.8 오프라인 템플릿

PocTrace와 같은 **WinForms 데스크톱 + Core 라이브러리** 앱을 인트라넷/에어갭에서 새로 만들 때 쓰는 골격입니다.

```
WinFormsNet48\
  nuget.config          ← C:\OfflineDev\nuget-packages 우선
  WinFormsOfflineTemplate.sln
  src\
    App.Core\           ← 업무·Excel·HTML (WinForms 금지)
    App.UI\             ← 화면만
```

---

## 1. 새 프로그램으로 복사

PocTrace 솔루션 루트에서:

```powershell
powershell -ExecutionPolicy Bypass -File .\offline-kit\templates\New-WinFormsAppFromTemplate.ps1 `
  -Name "PlantReport" `
  -Destination "C:\JCS\1. Codes\PlantReport"
```

또는 폴더를 통째로 복사한 뒤 솔루션/프로젝트 이름만 바꿉니다.

Visual Studio에서 `WinFormsOfflineTemplate.sln` 을 연 다음 **복원 → 빌드** 하면 됩니다.

전제:

- `C:\OfflineDev\nuget-packages` 가 이미 채워져 있을 것 (노트북에서 `Download-All-Offline.ps1` 실행한 상태)
- VS 2019/2022 + .NET Framework 4.8 타게팅 팩

---

## 2. 오프라인 키트를 쓰는 방법

### 2-1. 폴더 역할

| 경로 | 역할 |
|------|------|
| `C:\OfflineDev\nuget-packages` | **모든 C# 솔루션 공용** NuGet 캐시 |
| `C:\OfflineDev\web-assets` | HTML용 css/js/fonts/emoji 원본 |
| `C:\OfflineDev\models` | 나중에 `.onnx` 등 |

이 템플릿의 `nuget.config` 는 솔루션 루트에 있으며, restore 시 **OfflineDev를 먼저** 봅니다.

```xml
<add key="OfflineDev" value="C:\OfflineDev\nuget-packages" />
```

서안 인트라넷 PC에서는 **nuget.org 줄을 삭제**하세요. 인터넷이 없으면 restore가 nuget.org로 나가며 실패합니다.

### 2-2. 이미 캐시에 있는 패키지를 프로젝트에 쓰기

캐시에 있는 버전을 확인:

```
C:\OfflineDev\nuget-packages\EPPlus.7.2.1\
C:\OfflineDev\nuget-packages\Serilog.4.3.0\
C:\OfflineDev\nuget-packages\Dapper.2.1.66\
```

csproj에 같은 버전으로 추가:

```xml
<PackageReference Include="Dapper">
  <Version>2.1.66</Version>
</PackageReference>
```

그다음 VS에서 Restore 하거나:

```powershell
nuget restore WinFormsOfflineTemplate.sln -ConfigFile nuget.config
```

**인터넷이 필요 없습니다.** 버전이 캐시와 다르면 restore가 실패합니다. 새 버전을 쓰려면 온라인 PC에서 먼저 받아야 합니다.

### 2-3. “앞으로 쓸지도 모르는” 패키지

노트북(온라인)에서 PocTrace의 스크립트가 이미 받아 둔 것:

- 데이터: Dapper, Microsoft.Data.SqlClient, CsvHelper, ClosedXML, MiniExcel
- 로그/설정: Serilog.*, Microsoft.Extensions.*
- ML(온디바이스): Microsoft.ML, OnnxRuntime
- UI: WebView2, ScottPlot.WinForms, MetroModernUI
- 빌드: Costura.Fody, net48 ReferenceAssemblies

목록은 `offline-kit\Download-OfflinePackages.ps1` 의 `$currentPackages` / `$futurePackages` 입니다.

### 2-4. 캐시에 없는 새 패키지 (온라인 PC에서만)

1. 노트북에서 인터넷이 될 때:

```powershell
nuget install 패키지ID -Version x.y.z -OutputDirectory C:\OfflineDev\nuget-packages -Source https://api.nuget.org/v3/index.json
```

또는 스크립트 `$futurePackages` 배열에 `Id|Version` 한 줄을 넣고:

```powershell
.\offline-kit\Download-OfflinePackages.ps1 -Future
```

2. 그다음 **csproj에 PackageReference 추가**
3. `C:\OfflineDev` 를 다시 반입 ZIP에 넣기 (`Pack-AirgapTransfer`)

인트라넷에서 처음 보는 패키지 ID/버전을 적으면 **받을 곳이 없습니다.**

### 2-5. HTML/CSS/JS/폰트

런타임 CDN 금지. 리포트 HTML은 로컬 `assets/` 만 참조합니다.

```powershell
xcopy /E /I /Y C:\OfflineDev\web-assets\css   src\App.Core\Templates\assets\css
xcopy /E /I /Y C:\OfflineDev\web-assets\js    src\App.Core\Templates\assets\js
xcopy /E /I /Y C:\OfflineDev\web-assets\fonts src\App.Core\Templates\assets\fonts
```

`report.html` 의 bootstrap link 주석을 해제하면 됩니다.

JS 라이브러리를 새로 쓸 때도 동일: 온라인에서 `Download-OfflineWebAssets.ps1` 에 URL을 추가해 받은 뒤 `assets`로 복사.

---

## 3. 코딩 규칙 (PocTrace와 동일)

- **App.Core에 WinForms 참조 금지** — 검수/배치/엑셀은 Core
- **외부 REST/클라우드/LLM API 금지**
- Excel은 EPPlus, `LicenseContext.NonCommercial` (또는 조직 라이선스)
- exe 배포는 Costura.Fody 로 DLL 병합 (이미 UI 프로젝트에 포함)
- 로그는 `bin\...\logs\app-YYYYMMDD.txt`

---

## 4. 템플릿이 보여주는 흐름

1. UI 버튼 → `SampleJob.Run` (Core)
2. `SampleExcelExporter` → xlsx
3. `LocalHtmlWriter` → html (CDN 없음)
4. `out\` 폴더에 저장

이 세 파일을 실제 업무명으로 바꾸면 새 도구의 뼈대가 됩니다.

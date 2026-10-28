# Offline 공통 저장소 + PocTrace assets 매핑

> **에어갭 반입(인트라넷 → SVDI)** 은  
> [`airgap/AIRGAP_TRANSFER_GUIDE.md`](airgap/AIRGAP_TRANSFER_GUIDE.md)  
> 노트북 팩: `airgap\Pack-AirgapTransfer.bat`  
> 인트라넷: `airgap\Setup-IntranetDev.bat`  
> SVDI: `airgap\Setup-SvdiRuntime.bat`

## 권장 PC 공통 루트

```
C:\OfflineDev\
  nuget-packages\     # 모든 VS C# 솔루션 공용 NuGet
  web-assets\         # HTML CSS/JS/fonts/emoji 공용 원본
    css\
    js\
    fonts\
    emoji\
    vendor-meta\      # 다운로드 로그
  models\             # *.onnx 등
  vs-layout\          # VS Build Tools 오프라인 (선택)
```

프로젝트 `offline-kit\` = **스크립트·문서**. 바이너리 원본은 `C:\OfflineDev`.

새 WinForms(.NET 4.8) 앱 골격: [`templates/WinFormsNet48/README.md`](templates/WinFormsNet48/README.md)

---

## 한 번에 받기 (온라인 PC)

솔루션 루트에서:

```powershell
cd "C:\JCS\1. Codes\260718\PocTraceSystem"

# NuGet(현재+향후) + CSS/JS/fonts/emoji 전부
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-All-Offline.ps1

# 프로젝트 Templates\assets 까지 복사
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-All-Offline.ps1 -CopyToProject
```

### NuGet만

```powershell
# 현재 사용 중 + 향후 확장용 전부
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflinePackages.ps1 -All

# 향후 확장용만
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflinePackages.ps1 -Future
```

`nuget.exe` 필요: `winget install Microsoft.NuGet` 또는 `C:\Tools\nuget\nuget.exe`

### Web assets만

```powershell
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1 -CopyToProject

# layout 필수만 (용량·시간 절약)
powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1 -EssentialOnly
```

---

## 이모지(📊🔧)가 HTML에서 깨지는 이유

Pretendard / MiSans에는 **컬러 이모지 글리프가 없음**.  
스크립트가 넣는 것:

| 파일 | 역할 |
|------|------|
| `fonts/NotoColorEmoji.woff` | Unicode 이모지 폰트 폴백 |
| `css/emoji-support.css` | `@font-face` + font-stack 힌트 |
| `emoji/twemoji.min.js` + `js/twemoji-init.js` | 이모지→이미지 치환(권장) |

`layout.html`에 (로컬 경로만):

```html
<link rel="stylesheet" href="assets/css/emoji-support.css" />
<!-- body 하단 -->
<script src="assets/emoji/twemoji.min.js"></script>
<script src="assets/js/twemoji-init.js"></script>
```

완전 에어갭이면 Twemoji SVG zip을 `web-assets/emoji/svg/`에 풀어 두고 `twemoji-init.js`의 `base`를 `assets/emoji/`로 바꾸세요.

---

## Assets → 런타임

```
PocTrace.Core\Templates\assets\
  css\  js\  fonts\  emoji\
```

```powershell
Copy-Item -Recurse -Force C:\OfflineDev\web-assets\css\*   ".\PocTrace.Core\Templates\assets\css\"
Copy-Item -Recurse -Force C:\OfflineDev\web-assets\js\*    ".\PocTrace.Core\Templates\assets\js\"
Copy-Item -Recurse -Force C:\OfflineDev\web-assets\fonts\* ".\PocTrace.Core\Templates\assets\fonts\"
Copy-Item -Recurse -Force C:\OfflineDev\web-assets\emoji\* ".\PocTrace.Core\Templates\assets\emoji\"
```

또는 `-CopyToProject` 스위치 사용.

### layout.html 필수 JS/CSS

**css:** bootstrap.min.css, dataTables.bootstrap5.min.css, style.css (+ bootstrap-icons, emoji-support)  
**js:** jquery, bootstrap.bundle, echarts, jszip, lodash, dayjs, dataTables*, lang.js, app.js

---

## NuGet 향후 패키지(스크립트 -Future에 포함)

DI/Config, Microsoft.Data.SqlClient, CsvHelper, ClosedXML, MessagePack, Polly, FluentValidation, ONNX Managed, System.Text.Json,  
WebView2, ScottPlot, MiniExcel, AngleSharp, net48 ReferenceAssemblies, Encoding.CodePages, Quartz JSON 등.  
**채택 전까지 csproj에 넣지 말 것** — 오프라인 캐시만 미리 확보.

Twemoji SVG는 `C:\OfflineDev\web-assets\emoji\svg\` 에 풀어 두고 `twemoji-init.js`는 `assets/emoji/` 로컬 base를 사용합니다.

## Jcs.UiAssets (웹 유리 UI)

PocTrace 웹과 다른 프로그램이 같이 쓰는 아이콘·마크·`jcs-glass.css`입니다. 런타임 CDN은 없습니다.

- 원본: `shared/Jcs.UiAssets`
- 아이콘: Lucide 0.544.0 (ISC), 저장소에 포함
- 마크 png/ico: 원본 드로잉
- 패키지 이름: `Jcs.UiAssets` `1.0.0` (`offline-kit\nuget-packages\Jcs.UiAssets.1.0.0.nupkg`)
- 인트라넷 설치: `offline-kit\airgap\Install-JcsUiAssets.bat` → `C:\OfflineDev\nuget-packages`
- 반입 ZIP: `dist\Jcs.UiAssets-1.0.0-intranet.zip`
- 다시 만들기: `nuget pack shared\Jcs.UiAssets\Jcs.UiAssets.nuspec -OutputDirectory offline-kit\nuget-packages`

웹은 `PocTrace.Web\wwwroot\assets\jcs-ui` 복사본을 제공합니다.

---

## 제약

- 런타임 CDN 금지. 받은 파일만 `assets/`에서 참조.
- 외부 LLM/클라우드 API NuGet·JS 추가 금지.

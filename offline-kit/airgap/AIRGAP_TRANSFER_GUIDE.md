# PocTrace 에어갭 반입 · 원클릭 셋업 가이드

노트북(온라인) → **회사 인트라넷 PC**(VS·컴파일) → **보안 SVDI**(실DB·스케줄러·공용폴더) 로 옮기는 절차입니다.

관련 스크립트는 모두 `offline-kit\airgap\` 에 있습니다.

| 스크립트 | 실행 위치 | 역할 |
|----------|-----------|------|
| `Pack-AirgapTransfer.ps1` | **노트북** | 반입 ZIP 생성 |
| `Setup-IntranetDev.bat` | **인트라넷 PC** | 소스+NuGet 원클릭 복원·빌드 |
| `Setup-SvdiRuntime.bat` | **SVDI** | 공용폴더 런타임·리포트 경로 원클릭 배치 |

---

## 0. 환경 역할 정리

```
[노트북] 코드 개발 + NuGet/JS/CSS 사전 다운로드
    │  메일/USB 단방향 반입 (ZIP)
    ▼
[인트라넷 PC] Visual Studio로 컴파일·수정 (인터넷/CDN 불가, nuget.org 차단 가정)
    │  Release 산출물 또는 동일 ZIP 재반입
    ▼
[보안 SVDI] 실DB 접속, Admin 스케줄러, X:\jHOOK 공용폴더에 캐시·HTML/Excel 출력
```

| 구분 | 인트라넷 PC | SVDI |
|------|-------------|------|
| VS / MSBuild | 있음 | 없을 수 있음 (빌드본만 배치 가능) |
| NuGet restore | **필수** (로컬 캐시만) | 불필요(이미 빌드된 exe) |
| 실DB | 보통 없음/제한 | **있음** |
| 공용폴더 | 선택 | `X:\jHOOK\...` **필수** |
| HTML assets | Templates에 포함 → 빌드 시 출력 | DailyReport에 DeployAssets |

기본 경로 (App.config와 동일, 변경 시 스크립트 파라미터로 지정):

- 캐시: `X:\jHOOK\PocTraceData`
- 리포트: `X:\jHOOK\DailyReport`

---

## 0-1. SVDI에서 수정해야 할 Config 파일 (정확한 이름)

빌드 후 `bin\Release\`(또는 `bin\Debug\`)에 여러 config가 보이지만, **런타임에 실제로 읽는 파일은 exe.config 하나**입니다.

| 프로그램 | SVDI에서 편집할 파일 (필수) | 개발 시 원본 (Beyond Compare) |
|----------|------------------------------|----------------------------------|
| **Admin** | `PocTrace.Admin.exe.config` | `PocTrace.Admin\App.config` |
| **Viewer (UI)** | `PocTrace.UI.exe.config` | `PocTrace.UI\App.config` |

### SVDI에서 무시해도 되는 파일

| 파일 | 이유 |
|------|------|
| `App.config` (bin 안) | 빌드 부산물 복사본. **실행 시 사용 안 함** |
| `PocTrace.Core.dll.config` | Core 라이브러리용 복사본. **exe가 host이면 ConfigurationManager는 exe.config만 읽음** |
| `obj\Debug\*.exe.config` | MSBuild 중간 산출물 |
| `PocTrace.Admin.dll.config` | 없음 (WinExe) |

### Admin `PocTrace.Admin.exe.config` 필수 항목

- `appSettings`: `SharedCachePath`, `DailyReportPath`, `TargetDatabases`, `CacheMaxAgeHours`, `MaxParallelTrace`
- `connectionStrings`: **`PocTraceDB`**, **`SourceDb`** (DailyTraceJob은 `PocTraceDB` 사용)

### Viewer `PocTrace.UI.exe.config` 필수 항목

- `appSettings`: `SharedCachePath`, `DailyReportPath`, `TargetDatabases`, **`AllowDatabaseFallback=false`**, `CacheMaxAgeHours`
- `connectionStrings`: `SourceDb` (fallback 끄면 DB 미사용 가능)

### Beyond Compare 팁

1. 인트라넷: `App.config`(소스) 수정 → Rebuild → `exe.config` 생성 확인  
2. SVDI: **`PocTrace.Admin.exe.config` / `PocTrace.UI.exe.config`만** 배포·편집  
3. `Core.dll.config` / bin의 `App.config`는 비교·동기화 대상에서 제외

---

## 1. 노트북에서 ZIP 만들기 (원클릭)

솔루션 루트에서:

```powershell
cd "C:\JCS\1. Codes\260718\PocTraceSystem"
powershell -ExecutionPolicy Bypass -File .\offline-kit\airgap\Pack-AirgapTransfer.ps1
```

기본 출력: `C:\OfflineDev\transfer\PocTrace-Airgap-YYYYMMDD\`

| ZIP | 내용 | 비고 |
|-----|------|------|
| `01-PocTrace-Source.zip` | 소스 + Templates/assets + airgap 스크립트 | **항상 반입** |
| `02-PocTrace-NuGet.zip` | `C:\OfflineDev\nuget-packages` | 용량 큼. 인트라넷 빌드에 **필수** |
| `03-PocTrace-WebAssets.zip` | `C:\OfflineDev\web-assets` (선택) | Templates에 이미 있으면 생략 가능 |

옵션:

```powershell
# NuGet 제외 (이미 반입된 경우)
...\Pack-AirgapTransfer.ps1 -SkipNuGet

# 출력 폴더 지정
...\Pack-AirgapTransfer.ps1 -OutDir "D:\Transfer\PocTrace"
```

**메일 용량 제한**이 있으면 `01` / `02` 를 나눠 보내고, SVDI에는 나중에 Release 폴더만 별도 ZIP해도 됩니다.

### ZIP에 넣지 않는 것

- `bin\`, `obj\`, `.vs\`, `.git\`
- `agent-transcripts`, 개인 로그
- DB 비밀번호가 들어간 **실제** App.config (템플릿만 포함)
- 노트북 로컬 DB 덤프 (SVDI에서 실DB 사용)

---

## 2. 인트라넷 PC 셋업 (원클릭)

### 2-1. 반입·압축 해제

권장 구조:

```
D:\PocTrace\
  transfer\                 ← ZIP 3개를 여기 둠
    01-PocTrace-Source.zip
    02-PocTrace-NuGet.zip
    03-PocTrace-WebAssets.zip   (선택)
  Setup-IntranetDev.bat     ← Source ZIP 안의 airgap\ 에서 꺼내도 됨
```

또는 Source ZIP만 먼저 풀면:

```
D:\PocTrace\PocTraceSystem\
  offline-kit\airgap\Setup-IntranetDev.bat
```

### 2-2. 원클릭 실행

`Setup-IntranetDev.bat` 더블클릭, 또는:

```powershell
powershell -ExecutionPolicy Bypass -File .\Setup-IntranetDev.ps1 `
  -TransferDir "D:\PocTrace\transfer" `
  -TargetRoot "D:\PocTrace\PocTraceSystem" `
  -OfflineDevRoot "C:\OfflineDev"
```

스크립트가 하는 일:

1. `01` ZIP → `TargetRoot` 소스 전개  
2. `02` ZIP → `C:\OfflineDev\nuget-packages`  
3. (있으면) `03` → `C:\OfflineDev\web-assets`  
4. `nuget.config` 를 **OfflineDev만** 쓰도록 에어갭용으로 교체  
5. `nuget restore` (로컬 폴더만)  
6. MSBuild Release (VS 설치 경로 자동 탐색)  
7. `config\App.config.*.example` 안내  

### 2-3. 수동 확인 (1회)

1. Visual Studio에서 `PocTraceSystem.sln` 열기 → 빌드 성공 확인  
2. **Admin / Viewer / Core** 의 `App.config`  
   - 인트라넷에서 DB가 없으면 connectionString은 더미 유지 가능  
   - SVDI용 값은 SVDI에서만 실서버로 수정  
3. CDN·nuget.org 호출이 없는지 (방화벽/프록시 로그)

---

## 3. SVDI 런타임 셋업 (원클릭)

두 가지 중 하나:

**A. 인트라넷에서 빌드한 Release를 ZIP**  
`PocTrace.Admin\bin\Release`, `PocTrace.UI\bin\Release`(Viewer) 를  
`04-PocTrace-Runtime.zip` 등으로 묶어 SVDI에 반입.

**B. SVDI에 VS가 있으면** Source+NuGet을 동일하게 셋업 후 빌드.

### 3-1. 원클릭 배치

```powershell
powershell -ExecutionPolicy Bypass -File .\Setup-SvdiRuntime.ps1 `
  -RuntimeZip "D:\in\04-PocTrace-Runtime.zip" `
  -SharedRoot "X:\jHOOK" `
  -DeployRoot "X:\jHOOK\Apps\PocTrace"
```

또는 bat:

```bat
Setup-SvdiRuntime.bat
```

스크립트가 하는 일:

1. 공용 폴더 생성  
   - `X:\jHOOK\PocTraceData` (BinaryCache)  
   - `X:\jHOOK\DailyReport` (HTML/Excel)  
   - `X:\jHOOK\Apps\PocTrace\Admin`, `...\Viewer`  
2. Runtime ZIP 또는 `-AdminBin` / `-ViewerBin` 경로에서 exe·dll·Templates 복사  
3. `App.config` 에 SharedCachePath / DailyReportPath 반영  
4. `App.config.svdi.example` 기준으로 **connectionString은 직접 수정** 안내  
5. 최초 `DeployAssets` 는 Admin 1회 실행 시 자동 (Templates → DailyReport\assets)

### 3-2. SVDI에서 반드시 수동으로 할 일

1. **Admin** `PocTrace.Admin.exe.config`  
   - `SourceDb` / `PocTraceDB` → 실DB 서버·계정  
   - `TargetDatabases` → `x1,x2,x2_2` (운영 라인)  
2. **Viewer** 동일 캐시 경로, `AllowDatabaseFallback=false` 권장(캐시만)  
3. Admin 실행 → 수동 Trace 1회 →  
   - `X:\jHOOK\PocTraceData\GraphCache_*` 생성 확인  
   - `X:\jHOOK\DailyReport\` HTML·Excel·assets 확인  
4. Quartz 스케줄(기존 Admin UI)로 공용폴더 기준 일배치 확인  
5. HTML은 `DailyReport`를 브라우저로 열기 (CDN 없이 `./assets/...`)

---

## 4. 파일 매핑 요약 (무엇을 어디로)

| 노트북 | 인트라넷 | SVDI |
|--------|----------|------|
| 소스 트리 | `D:\...\PocTraceSystem\` | (빌드 시) 또는 불필요 |
| `C:\OfflineDev\nuget-packages` | 동일 경로 복원 | 불필요 |
| `Templates\assets` (js/css/fonts) | 소스에 포함 → 빌드 출력 | Admin Deploy → `DailyReport\assets` |
| `App.config` 시크릿 | example만 | **실DB 값만 SVDI에** |
| BinaryCache | 없음 | `X:\jHOOK\PocTraceData` |
| HTML/Excel | 미리보기만 | `X:\jHOOK\DailyReport` |

---

## 5. 체크리스트

### 노트북 Pack 전

- [ ] `Download-All-Offline.ps1` 로 NuGet·web-assets 최신화  
- [ ] Templates `poc-*.js`, `app.js`, `lang.js`, echarts 등 포함 확인  
- [ ] App.config에 실비밀번호 없는지 확인  

### 인트라넷

- [ ] `Setup-IntranetDev` 성공, Restore+Build OK  
- [ ] nuget.org 없이 복원됨  
- [ ] Release 산출물 ZIP 준비  

### SVDI

- [ ] `X:\jHOOK` 쓰기 권한  
- [ ] DB 연결 문자열  
- [ ] Admin 1회 실행 → 캐시·DailyReport 생성  
- [ ] Viewer가 캐시 읽고 Trace  
- [ ] HTML Ctrl+F5, 콘솔 CDN/404 없음  

---

## 6. 문제 해결

| 증상 | 조치 |
|------|------|
| NuGet restore 실패 | `02` ZIP 전개 경로가 `C:\OfflineDev\nuget-packages` 인지, nuget.config에 nuget.org 없는지 |
| Templates/assets 누락 | Source ZIP에 `PocTrace.Core\Templates` 포함 여부, Admin DeployAssets |
| HTML 아이콘/JS 깨짐 | DailyReport `assets` 가 Templates와 동기화됐는지, `poc-*.js` 로드 순서 |
| Viewer 캐시 거부 | 48h 게이트 / Admin 재생성, SharedCachePath 일치 |
| 스케줄만 실패 | SVDI에서 Admin 프로세스·공용폴더 권한·로그 폴더 |

추가 검증(노트북):

```powershell
powershell -ExecutionPolicy Bypass -File .\offline-kit\_Verify-OfflineKit.ps1
```

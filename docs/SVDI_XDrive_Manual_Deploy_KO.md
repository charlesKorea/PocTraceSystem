# SVDI 경로 분리: `D:\PocAdmin` (개인) vs `X:\jHOOK` (공용)

코드·config 기본값 기준. Admin은 **개인 D:** 에서만 실행하고, 캐시·HTML·Viewer만 **공용 X:** 에 둡니다.

---

## 1. 역할 한눈에

| 구분 | 루트 | 역할 | 누가 씀 |
|------|------|------|---------|
| 개인 | `D:\PocAdmin\` | Admin 실행, Templates(SoT), DB config, 개인 로그 | **본인만** |
| 공용 | `X:\jHOOK\` | BinaryCache, DailyReport(HTML+assets+Excel), Viewer | 전원 |

```
D:\PocAdmin\  (Admin.exe + Templates)
       │ Full
       ├──────────────► X:\jHOOK\PocTraceData\     캐시 쓰기
       └──────────────► X:\jHOOK\DailyReport\      HTML/Excel/assets 쓰기
                              ▲
X:\jHOOK\Apps\PocTrace\Viewer\ ──읽기── PocTraceData
브라우저 ──읽기── DailyReport
```

Admin `PocTrace.Admin.exe.config` 필수 키:

| 키 | 값 |
|----|-----|
| SharedCachePath | `X:\jHOOK\PocTraceData` |
| DailyReportPath | `X:\jHOOK\DailyReport` |

---

## 2. 전체 폴더·파일 구성표

### 2-A. 개인 — `D:\PocAdmin\`

| 경로 | 종류 | 필수 | 설명 |
|------|------|------|------|
| `D:\PocAdmin\PocTrace.Admin.exe` | exe | ✅ | Admin 본체 |
| `D:\PocAdmin\PocTrace.Admin.exe.config` | config | ✅ | 경로·DB·TargetDatabases (**공용에 올리지 말 것**) |
| `D:\PocAdmin\PocTrace.Core.dll` | dll | ✅ | 엔진·검수·HTML 생성 |
| `D:\PocAdmin\JLic.Core.dll` | dll | ✅ | 라이선스 검증 |
| `D:\PocAdmin\Lic\jlab.lic` | lic | ✅ | Admin은 Admin 그룹 + `PT` 또는 `AL` |
| `D:\PocAdmin\*.dll` | dll | ✅ | 의존 패키지 전부 (EPPlus, Serilog, Dapper, Costura 병합 여부에 따라 상이) |
| `D:\PocAdmin\Templates\layout.html` | 템플릿 | ✅ | HTML 뼈대 SoT → Full 시 공용으로 복사 |
| `D:\PocAdmin\Templates\assets\css\` | 폴더 | ✅ | style.css, bootstrap*.css, dataTables*.css, bootstrap-icons*.css 등 |
| `D:\PocAdmin\Templates\assets\js\` | 폴더 | ✅ | app.js, lang.js, poc-*.js, echarts, jquery 등 |
| `D:\PocAdmin\Templates\assets\fonts\` | 폴더 | ✅ | bootstrap-icons 폰트 등 |
| `D:\PocAdmin\Templates\assets\emoji\` | 폴더 | 선택 | twemoji 등 |
| `D:\PocAdmin\logs\` 또는 Serilog 경로 | 로그 | 권장 | 개인 로컬 |

**여기에 두지 않음:** Viewer exe, `data_*.js`, `report_*.html`, BinaryCache bin (그건 X:).

---

### 2-B. 공용 — `X:\jHOOK\PocTraceData\` (캐시)

| 경로 | 종류 | 누가 씀 | 설명 |
|------|------|---------|------|
| `X:\jHOOK\PocTraceData\` | 폴더 | Admin 쓰기 / Viewer 읽기 | SharedCachePath |
| `X:\jHOOK\PocTraceData\GraphCache_x1\` | 폴더 | Admin Full·CacheOnly 생성 | DB `x1` |
| `...\GraphCache_x1\manifest.bin` | bin | | v5 매니페스트 |
| `...\GraphCache_x1\nodes.bin` | bin | | |
| `...\GraphCache_x1\pocs.bin` | bin | | |
| `...\GraphCache_x1\pipes.bin` | bin | | |
| `...\GraphCache_x1\takeoffs.bin` | bin | | |
| `...\GraphCache_x1\dynprops.bin` | bin | | Hookup 등 |
| `...\GraphCache_x2\` … | 동일 | | TargetDatabases에 있는 라인만 |

(레거시 `GraphCache_{db}.bin` 단일 파일이 있으면 읽기만·Admin 저장 시 v5 폴더로 전환)

---

### 2-C. 공용 — `X:\jHOOK\DailyReport\` (HTML·자산·Excel)

| 경로 | 종류 | 생성/갱신 | 설명 |
|------|------|-----------|------|
| `X:\jHOOK\DailyReport\` | 폴더 | — | DailyReportPath, 브라우저로 열기 |
| `...\layout.html` | html | Admin Full `DeployAssets` | Templates에서 덮어씀 |
| `...\index.html` | html | Admin Full | 라인 통합 Summary |
| `...\report_x1.html` | html | Admin Full | 라인별 리포트 (db명 대응) |
| `...\data_x1.js` | js(데이터) | Admin Full | 대용량 JSON 스트리밍 산출물 |
| `...\rules.html` | html | Admin Full | Check Rules |
| `...\data_rules.js` 등 | js(데이터) | Admin Full | rules 데이터 |
| `...\progress.html` | html | Admin Full (Progress on) | 설계 진행률 |
| `...\data_progress_*.js` | js(데이터) | Admin Full | progress 데이터 |
| `...\assets\` | 폴더 | DeployAssets | **열람용** CSS/JS/fonts (공용에 필수) |
| `...\assets\css\` | | | 아래 앱·라이브러리 css |
| `...\assets\js\` | | | 아래 앱·라이브러리 js |
| `...\assets\fonts\` | | | 아이콘 폰트 |
| `...\Excel\` | 폴더 | Admin Full | |
| `...\Excel\x1_Full_List.xlsx` | xlsx | Admin Full | |
| `...\Excel\x1_Error_Detail.xlsx` | xlsx | Admin Full | 이슈 있을 때 |
| `...\Excel\x1_Hookup_Validation.xlsx` | xlsx | Admin Full | Hookup 대상 시 |

**수동으로 넣지 말 것(Admin이 만듦):** `data_*.js`, `report_*.html`, `index.html`, Excel.  
**수동/배포로 맞출 것:** `assets\**`, 필요 시 `layout.html` (핫픽스).

---

### 2-D. 공용 — Viewer

| 경로 | 종류 | 필수 | 설명 |
|------|------|------|------|
| `X:\jHOOK\Apps\PocTrace\Viewer\PocTrace.UI.exe` | exe | ✅ | Viewer (경로명은 현장 관례에 맞게 조정 가능) |
| `...\PocTrace.UI.exe.config` | config | ✅ | `SharedCachePath=X:\jHOOK\PocTraceData`, `AllowDatabaseFallback=false` 권장 |
| `...\PocTrace.Core.dll` 및 의존 dll | dll | ✅ | |
| `...\JLic.Core.dll` | dll | ✅ | 라이선스 검증. Viewer/Web/철거/노즐/Admin 각 폴더에 필요 |
| `...\Lic\jlab.lic` | lic | ✅ | `JLicManager` 가 만든 파일. 발급기·`Keys\issuer.keys` 는 두지 말 것 |
| Viewer용 Templates | | 보통 불필요 | HTML은 DailyReport가 담당. Viewer는 캐시 Trace UI |

### 2-E. 공용 — Web (Viewer와 별도 폴더)

| 경로 | 종류 | 필수 | 설명 |
|------|------|------|------|
| `X:\jHOOK\Apps\PocTrace\Web\PocTrace.Web.exe` | exe | ✅ | 사용자마다 실행. `127.0.0.1` 만 연다 |
| `...\PocTrace.Web.exe.config` | config | ✅ | Viewer와 같은 `SharedCachePath`, `TargetDatabases`. `AllowDatabaseFallback=false` |
| `...\wwwroot\` | html | ✅ | 브라우저 화면 |
| `...\JLic.Core.dll` | dll | ✅ | |
| `...\Lic\jlab.lic` | lic | ✅ | AccessApps에 `PT` 또는 `AL` |
| `...\UtMapping.csv` | csv | 권장 | 없으면 Trace는 되나 UT 매핑이 비어 있음 |

**공용에 두지 않음:** `PocTrace.Admin.exe`, Admin용 DB connectionString config, `JLicManager.exe`, `Keys\issuer.keys`.

---

## 3. Templates / DailyReport assets — 파일 단위 표

노트북 SoT: `PocTrace.Core\Templates\`  
→ 개인: `D:\PocAdmin\Templates\`  
→ Full 또는 수동 복사 → 공용: `X:\jHOOK\DailyReport\`

### 3-A. 루트·레이아웃

| 노트북 | 개인 (D:) | 공용 (X:) |
|--------|-----------|-----------|
| `...\Templates\layout.html` | `D:\PocAdmin\Templates\layout.html` | `X:\jHOOK\DailyReport\layout.html` |

### 3-B. 앱 JS (자주 갱신)

| 파일 | 개인 | 공용 |
|------|------|------|
| `app.js` | `D:\PocAdmin\Templates\assets\js\app.js` | `X:\jHOOK\DailyReport\assets\js\app.js` |
| `lang.js` | `...\Templates\assets\js\lang.js` | `...\DailyReport\assets\js\lang.js` |
| `poc-core.js` | 동일 패턴 | 동일 패턴 |
| `poc-charts.js` | | |
| `poc-grid.js` | | |
| `poc-similar.js` | | |
| `poc-action.js` | | |
| `poc-rules.js` | | |
| `poc-progress.js` | | ⚠ Full 휘발성 목록 **미포함** → 핫픽스 시 **공용에도 직접** 복사 |

### 3-C. 주요 라이브러리 JS (최초·버전 업 시)

| 파일 (예) | 개인 `Templates\assets\js\` | 공용 `DailyReport\assets\js\` |
|-----------|------------------------------|-------------------------------|
| `echarts.min.js` | ✅ | ✅ |
| `jquery.min.js` | ✅ | ✅ |
| `bootstrap.bundle.min.js` | ✅ | ✅ |
| `jquery.dataTables.min.js` / `dataTables*.js` | ✅ | ✅ |
| `jszip.min.js`, `lodash.min.js`, `dayjs.min.js` | ✅ | ✅ |
| `buttons.html5.min.js` 등 | ✅ | ✅ |

### 3-D. CSS / fonts

| 파일·폴더 | 개인 | 공용 |
|-----------|------|------|
| `assets\css\style.css` | `D:\PocAdmin\Templates\...` | `X:\jHOOK\DailyReport\...` |
| `bootstrap.min.css`, `bootstrap-icons*.css` | 동일 | 동일 |
| `dataTables*.css`, `buttons.dataTables.min.css` | 동일 | 동일 |
| `assets\fonts\*` | 동일 | 동일 |

---

## 4. Admin config vs Viewer config

| 설정 | `D:\PocAdmin\PocTrace.Admin.exe.config` | 공용 Viewer `*.exe.config` |
|------|------------------------------------------|----------------------------|
| SharedCachePath | `X:\jHOOK\PocTraceData` | **동일** |
| DailyReportPath | `X:\jHOOK\DailyReport` | 있어도 되고, Viewer는 주로 캐시만 |
| SourceDb / PocTraceDB | **실서버 (개인만)** | 넣지 않음 / Fallback false |
| TargetDatabases | `x1,x2,...` | Viewer도 라인 목록 필요 시 동일 |
| AllowDatabaseFallback | true 가능(Admin) | **false 권장** |
| DesignProgress* | Admin Full용 | — |

---

## 5. 작업별 “어디를 손대나”

| 하고 싶은 일 | D:\PocAdmin | X:\jHOOK |
|--------------|-------------|----------|
| UI JS/CSS 핫픽스 (당장 전원 반영) | `Templates\assets\...` 갱신 | `DailyReport\assets\...` **같이** 갱신 |
| 다음 Full부터만 맞추면 됨 | `Templates\`만 갱신 | Full 한 번 돌리면 휘발성 파일 반영 |
| 검수/Trace 로직 변경 | exe·dll·Templates 교체 후 **Full** | 캐시·HTML 자동 갱신 |
| 캐시만 신선 (48h) | Admin **CacheOnly** | `PocTraceData`만 변경, DailyReport 유지 |
| Viewer만 배포 | — | `Apps\PocTrace\Viewer\` 교체 (`JLic.Core.dll`, `Lic\jlab.lic` 포함) |
| Web만 배포 | — | `Apps\PocTrace\Web\` 교체 (Viewer 덮어쓰지 말 것) |
| 리포트 데이터 확인 | — | `DailyReport\index.html` 등 열기 |

---

## 6. Admin Full / CacheOnly → X: 영향

| 모드 | `D:\PocAdmin` | `X:\jHOOK\PocTraceData` | `X:\jHOOK\DailyReport` |
|------|---------------|-------------------------|------------------------|
| **Full** | 실행·로그 | GraphCache_* 저장 | DeployAssets + HTML/Excel/Rules/Progress |
| **CacheOnly** | 실행·로그 | GraphCache_* 저장 | **변경 없음** |

`DeployAssets` 입력 = **`D:\PocAdmin\Templates`**  
출력 = **`X:\jHOOK\DailyReport\assets` (+ layout.html)**

---

## 7. 트리 요약 (복사용)

```
D:\PocAdmin\                          ← 개인 Admin만
  PocTrace.Admin.exe
  PocTrace.Admin.exe.config           ← SharedCache/DailyReport → X:
  PocTrace.Core.dll
  JLic.Core.dll
  Lic\jlab.lic
  (기타 dll)
  Templates\
    layout.html
    assets\
      css\
      js\          ← app.js, lang.js, poc-*.js, echarts...
      fonts\
      emoji\       (선택)

X:\jHOOK\                             ← 공용
  PocTraceData\
    GraphCache_x1\   (*.bin)
    GraphCache_x2\   ...
  DailyReport\
    layout.html
    index.html
    report_*.html
    data_*.js
    rules.html
    progress.html
    assets\          ← 열람용 (Templates에서 배포)
      css\  js\  fonts\
    Excel\
  Apps\PocTrace\
    Viewer\          ← PocTrace.UI.exe (+ config, dll, JLic.Core.dll, Lic\jlab.lic)
    Web\             ← PocTrace.Web.exe (+ wwwroot, JLic.Core.dll, Lic\jlab.lic)
    (Admin 없음, JLicManager 없음)
```

---

## 8. 노트북 → SVDI 복사 한 줄

| 노트북 | → 개인 | → 공용 |
|--------|--------|--------|
| `PocTrace.Admin\bin\Release\*` (+Templates) | `D:\PocAdmin\` (+ `JLic.Core.dll`, `Lic\jlab.lic`) | (넣지 않음) |
| `PocTrace.UI\bin\Release\*` | (넣지 않음) | `X:\jHOOK\Apps\PocTrace\Viewer\` (+ `JLic.Core.dll`, `Lic\jlab.lic`) |
| `PocTrace.Web\bin\Release\*` (+wwwroot) | (넣지 않음) | `X:\jHOOK\Apps\PocTrace\Web\` (+ `JLic.Core.dll`, `Lic\jlab.lic`) |
| `PocTrace.Core\Templates\assets\**` 핫픽스 | `D:\PocAdmin\Templates\assets\` | `X:\jHOOK\DailyReport\assets\` |

---

## 9. SVDI에서 `Check Rules` / `설계 진행률` 메뉴가 안 보일 때

### 9-1. 원인 체크 (우선순위)

| 점검 항목 | 확인 방법 | 영향 |
|-----------|-----------|------|
| `layout.html` 구버전 | `X:\jHOOK\DailyReport\layout.html`에 `rules.html`, `progress.html` 링크가 있는지 확인 | 네비 메뉴 자체가 사라짐 |
| Admin이 CacheOnly만 실행 | Admin 로그에서 `Mode=CacheOnly` 확인 | `rules.html`/`progress.html` 재생성 안 됨 |
| `DesignProgressEnabled=false` | `D:\PocAdmin\PocTrace.Admin.exe.config` | `progress.html` 생성 안 됨 |
| 공용 `assets/js` 구버전 | `app.js`, `lang.js`, `poc-rules.js`, `poc-progress.js` 날짜/버전 확인 | 메뉴 텍스트/렌더링 불일치 |

### 9-2. 어떤 파일을 어디에 업데이트?

#### A) 개인 Admin SoT (다음 Full 배치 기준)

| 소스(repo) | SVDI 대상 |
|------------|-----------|
| `PocTrace.Core\Templates\layout.html` | `D:\PocAdmin\Templates\layout.html` |
| `PocTrace.Core\Templates\assets\js\app.js` | `D:\PocAdmin\Templates\assets\js\app.js` |
| `PocTrace.Core\Templates\assets\js\lang.js` | `D:\PocAdmin\Templates\assets\js\lang.js` |
| `PocTrace.Core\Templates\assets\js\poc-rules.js` | `D:\PocAdmin\Templates\assets\js\poc-rules.js` |
| `PocTrace.Core\Templates\assets\js\poc-progress.js` | `D:\PocAdmin\Templates\assets\js\poc-progress.js` |
| `PocTrace.Core\Templates\assets\css\style.css` (필요시) | `D:\PocAdmin\Templates\assets\css\style.css` |

#### B) 공용 즉시 반영 (Full 배치 전 긴급 패치)

| 소스(repo) | SVDI 대상 |
|------------|-----------|
| `PocTrace.Core\Templates\layout.html` | `X:\jHOOK\DailyReport\layout.html` |
| `PocTrace.Core\Templates\assets\js\app.js` | `X:\jHOOK\DailyReport\assets\js\app.js` |
| `PocTrace.Core\Templates\assets\js\lang.js` | `X:\jHOOK\DailyReport\assets\js\lang.js` |
| `PocTrace.Core\Templates\assets\js\poc-rules.js` | `X:\jHOOK\DailyReport\assets\js\poc-rules.js` |
| `PocTrace.Core\Templates\assets\js\poc-progress.js` | `X:\jHOOK\DailyReport\assets\js\poc-progress.js` |

> 주의: `poc-progress.js`는 DeployAssets의 휘발성 목록에 없어, 공용 `assets`가 이미 있으면 Full만으로 자동 갱신되지 않을 수 있음.

### 9-3. 생성 파일 체크

| 파일 | 생성 주체 | 생성 위치 |
|------|-----------|-----------|
| `rules.html` | Admin Full (`GenerateRulesPage`) | `X:\jHOOK\DailyReport\` |
| `data_rules.js` | Admin Full (`GenerateRulesPage`) | `X:\jHOOK\DailyReport\` |
| `progress.html` | Admin Full + `DesignProgressEnabled=true` (`GenerateProgressSummary`) | `X:\jHOOK\DailyReport\` |
| `data_progress.js` | Admin Full + `DesignProgressEnabled=true` | `X:\jHOOK\DailyReport\` |
| `report_progress_{db}.html` | DB별 Progress 리포트 생성 | `X:\jHOOK\DailyReport\` |
| `data_progress_{db}.js` | DB별 Progress 데이터 | `X:\jHOOK\DailyReport\` |

### 9-4. `Check Rules` / `설계 진행률` 데이터가 어디서 만들어지나

| 메뉴 | Admin 로직 | 입력 데이터 | 출력 |
|------|------------|-------------|------|
| Check Rules | `DailyTraceJob` → `ValidationRuleRegistry.GetCatalog(includeHits:true)` → `HtmlReportGenerator.GenerateRulesPage(...)` | 파이프라인 룰 hit (`ValidationPipeline.Execute`의 `RecordHit`), Hookup 비정상 수(`RecordHookupHits`) | `rules.html`, `data_rules.js`, `Rules_History.json` |
| 설계 진행률 | `DailyTraceJob`에서 DB별 `progressEngine.Run(db, cache, allResults)` + `GenerateProgressReport`, 마지막에 `GenerateProgressSummary(progressReports)` | Graph cache(nodes/pocs/pipes/takeoffs), trace 결과(`allResults`), 설정값(`DesignProgress*`, 선택적으로 FRM) | `report_progress_{db}.html`, `data_progress_{db}.js`, `Progress_History_{db}.json`, `progress.html`, `data_progress.js` |

### 9-5. 운영 절차 (권장)

1. 위 파일들을 **먼저 `D:\PocAdmin\Templates`에 반영**  
2. `D:\PocAdmin\PocTrace.Admin.exe.config`에서 `DesignProgressEnabled=true` 확인  
3. Admin을 **Full** 모드로 1회 실행 (CacheOnly 아님)  
4. `X:\jHOOK\DailyReport\`에서 `rules.html`, `progress.html` 생성 확인  
5. 브라우저 `Ctrl+F5`로 강력 새로고침

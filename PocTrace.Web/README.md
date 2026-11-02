# PocTrace.Web

Viewer exe와 **같이** 쓰는 1차 웹 화면입니다. Viewer를 끄지 않습니다.

## 누가 무엇을 하나

| 프로그램 | 어디서 | 하는 일 |
|----------|--------|---------|
| `PocTrace.Admin.exe` | `D:\PocAdmin` | 캐시 생성, DailyReport. 본인만 |
| `PocTrace.UI.exe` | 공유 폴더 | 각 사용자가 실행. 철거·노즐과 같은 Core.dll, 같은 캐시 |
| `PocTrace.Demolition.exe`, `PocTrace.NozzleTrace.exe` | 그 공유 폴더 | UI와 같은 캐시 |
| `PocTrace.Web.exe` | 그 공유 폴더 | 각 사용자가 실행. 자기 화면의 브라우저만 연다 |

실행 폴더와 DLL, DB 비밀번호는 `docs/SVDI-SHARED-RUN.md`를 따른다.

웹은 `127.0.0.1`만 엽니다. 다른 사람 세션에서 이 주소로 들어오지 않습니다. 그래서 Viewer와 같이, **사용자마다 exe를 한 번씩** 실행합니다.

캐시 파일(`X:\jHOOK\PocTraceData`)은 읽기만 합니다. Excel은 공용 드라이브가 아니라 그 사용자 PC의 `%LOCALAPPDATA%\PocTraceWeb\jobs\`에 저장합니다.

## 배포

1. Visual Studio에서 `PocTraceSystem.sln`의 **PocTrace.Web**을 Release로 빌드합니다. 산출물은 `PocTrace.Web\bin\Release\` 또는 `bin\x64\Release\` 입니다.
2. 아래 파일을 공유 폴더 한 곳에 둡니다. UI, 철거, 노즐 exe도 그 폴더이고 `PocTrace.Core.dll`은 그 빌드 하나뿐입니다. Admin은 `D:\PocAdmin`에 따로 두고, Core.dll도 그 빌드의 복사본을 D:에 둡니다.
3. `PocTrace.Web.exe.config`의 `SharedCachePath`, `TargetDatabases`를 Viewer config와 같게 맞춥니다. `AllowDatabaseFallback`은 `false`로 둡니다. DB 비밀번호는 넣지 않습니다.

화면은 CDN을 쓰지 않습니다. `wwwroot` 안의 CSS와 SVG가 하나라도 빠지면 유리 표면이나 아이콘이 비습니다. `wwwroot`는 exe와 같은 폴더에 두고, 그 안의 상대 경로를 유지합니다.

### exe 옆에 두는 파일

| 파일 | 필수 | 설명 |
|------|------|------|
| `PocTrace.Web.exe` | 예 | 웹 본체. JLic, Newtonsoft, Serilog, EPPlus(배치 Excel 업로드, 검수 기록 Excel)는 이 파일 안에 들어 있습니다 |
| `PocTrace.Web.exe.config` | 예 | `SharedCachePath`, `TargetDatabases`, `ListenPort`, `SharedHistoryPath`, `MemoryEstimateFactor`, `AutoExitOnBrowserClose`, `AutoExitGraceSeconds`, `AutoExitStaleSeconds` |
| `PocTrace.Core.dll` | 예 | 이 솔루션 빌드의 Core.dll 하나 |
| `PocTrace.Demolition.exe` | 예 | Costura 제외. Web이 프로세스를 새로 띄우지 않고 이 어셈블리를 불러 철거 검수를 합니다. EPPlus는 이 exe 안에 있습니다 |
| `PocTrace.NozzleTrace.exe` | 예 | Costura 제외. 노즐 역추적도 이 어셈블리를 프로세스 안에서 부릅니다 |
| `Lic\jlab.lic` | 예 | AccessApps에 `PT` 또는 `AL` |
| `UtMapping.csv` | 권장 | 없으면 Trace는 되고 UT 매핑만 비어 있습니다 |
| `wwwroot\` | 예 | 아래 트리 전체 |

폴더에 `JLic.Core.dll`, `Serilog.dll`, `Serilog.Sinks.File.dll`, `Newtonsoft.Json.dll`, `EPPlus.dll`, `Dapper.dll`, `Costura.dll`이 남아 있으면 지웁니다. 남아 있으면 exe 안에 넣은 복사본보다 그 파일이 먼저 열립니다. 철거·노즐의 `exe.config`는 Web이 그 exe를 따로 실행하지 않으므로 없어도 됩니다.

### wwwroot (UiAssets)

`jcs-glass.css`가 `icons\*.svg`를 상대 경로로 읽습니다. 아이콘만 다른 폴더에 두면 버튼 모양이 사라집니다.

```
wwwroot\
  index.html
  history.html
  assets\
    jcs-ui\
      jcs-glass.css
      brand\
        mark.png
        mark-180.png
        mark.ico
      icons\
        circle-alert.svg
        circle-check.svg
        circle-help.svg
        cpu.svg
        crosshair.svg
        database.svg
        download.svg
        hammer.svg
        history.svg
        layers.svg
        loader.svg
        moon.svg
        play.svg
        route.svg
        search.svg
        sun.svg
        upload.svg
```

`layers.svg`는 이 버전의 CSS가 읽지 않습니다. 폴더를 통째로 복사하면 같이 들어갑니다. 브랜드 파일 세 개와 CSS가 읽는 SVG 16개는 빼면 안 됩니다. CSS가 읽는 아이콘은 route, hammer, crosshair, history, moon, sun, database, circle-check, circle-alert, loader, search, download, play, circle-help, cpu, upload 입니다.

사용자가 `PocTrace.Web.exe`를 실행하면 콘솔 창이 남고 브라우저가 `http://127.0.0.1:5088/`을 엽니다. 콘솔 창을 닫으면 그 사용자의 웹만 멈춥니다.

포트 5088이 이미 쓰이면 5089부터 차례로 시도합니다.

화면은 라인을 고른 뒤 **캐시 불러오기**를 눌러야 그 라인 캐시를 메모리에 올립니다. 고르는 즉시 올리거나, 이전에 켜 둔 작업을 자동으로 이어 받지 않습니다. 언어와 라이트/다크는 그 브라우저와 이 PC의 `%LocalAppData%\PocTrace\Web\ui-prefs.json`에 남아 다음 실행에 적용됩니다. 끝난 검수는 같은 폴더의 `inspection-history.json`과 공용 폴더(`SharedHistoryPath`, 기본 `Y:\sds\999. data\PocTraceWebHistory`)에 함께 남고, 화면의 **검수 기록**에서 모든 사용자의 기록을 사용자·검수 방식·라인·기간·대상별로 조회하고 Excel로 내려받습니다. 공용 폴더에 닿지 않으면 이 PC 기록만 보이고 화면에 안내가 나옵니다. 어느 화면에도 공용 폴더 경로는 표시하지 않습니다. Admin 권한 사용자는 기록 표의 체크박스로 여러 행을 골라 삭제할 수 있고, 삭제는 공용 폴더의 `deleted.tomb` 표식과 실제 파일 정리로 모든 사용자에게 반영됩니다.

Web.exe는 같은 Windows 세션에서 하나만 실행됩니다. 이미 떠 있으면 안내 팝업을 띄우고 기존 화면을 엽니다. 브라우저 화면을 모두 닫으면(마지막 화면이 닫히고 `AutoExitGraceSeconds` 뒤, 작업 중이면 끝난 뒤) 자동으로 종료됩니다. 브라우저가 비정상 종료되면 `AutoExitStaleSeconds` 뒤에 종료합니다.

## 이 버전의 기능

시작 시 `JLic.Core` 로 라이선스를 검사합니다. 실패하면 웹을 열지 않습니다 (종료 코드 2). 권한 `2차` 는 배관 Trace만 되고, 철거·노즐 탭은 숨기고 API는 403입니다.

화면 위 탭 세 개입니다. 한 번에 작업은 하나만 돕니다.

- **배관 Trace:** 라인을 고르고 캐시 불러오기를 누른 뒤에 태그 하나, 배치, 설비 요약, Excel을 합니다. 48시간이 지난 라인은 거부합니다. 배치는 Excel A열(2행부터)의 EQPID를 올려 채울 수 있고, 진행은 원형 진행 표시(캐시 → Trace → Excel, Tag n/m, 경과 시간)로 보입니다.
- **검수 기록:** `/history.html`. 모든 사용자의 날짜, 사용자, 검수 방식, 라인, 검수대상. 조회 조건과 Excel 다운로드가 있습니다.
- **철거검수:** Excel 업로드 또는 `Site,공정,건설코드,EQPID` 붙여넣기. 공정과 건설코드는 생략 가능 (`X1,EQP-001`). 검증 그리드에서 잘못된 행은 노란 색으로 남고, 검수는 유효 행만 한다. 진행률, 남은 시간, 행별 추적 상태, 종합 표, Excel, HTML 요약을 보여 준다. 실행 중 취소할 수 있다. 기존 철거 exe와 같은 검수다.
- **노즐추적:** BIM / Instance / GUID. 기존 노즐 exe와 같은 역추적 후 Excel. 파일은 DailyReport 아래에도 남고, 화면에서는 사용자 PC 폴더 복사본을 받습니다

공통 화면 요소:

- 라인 이름 옆에 `DB sync 날짜`(Admin이 DB에서 캐시를 만든 시각)를 보여 줍니다.
- 캐시 불러오기 진행률은 단계(파일 읽기 → 그래프 준비 → 인덱스)마다 숫자가 올라가고, 한 단계가 길어도 화면이 천천히 움직입니다.
- 상단 메모리 표시: 실행 시 예상 메모리(캐시 폴더 크기 × `MemoryEstimateFactor` + 300MB), 가용 메모리. 메모리에 올린 뒤에는 이 프로세스 사용량을 보여 줍니다.
- 상단 `?` 버튼: 기능별 사용 가이드 팝업(한국어/中文).
- 중문 화면에서는 Nozzle, Duct, Damper, Excel, ID, Instance, Tag, Trace 같은 업종 용어를 영어로 둡니다.
- 표 규칙은 `docs/Web_Table_Layout_Guide_KO.md`.

한 번에 철거 입력은 2000행, Excel은 8MB까지다. 종합 표는 화면에서 500행까지 보여주고, 전체는 Excel과 HTML에 있다.

아직 없는 것: 배관 Trace와 노즐추적의 작업 취소, 여러 사람이 캐시 한 벌을 나눠 쓰는 서버.

## 철거검수에서 더 본 것

이번에 웹에 넣은 것은 WinForms에 있고 웹에 빠져 있던 입력·검증·진행·요약·취소다. 아래는 같이 보고 그대로 둔 것이다.

- 철거 검수는 WinForms와 같이 캐시가 48시간을 넘어도 경고만 하고 진행한다. 배관 Trace만 그 시간을 넘으면 거절한다. 검증 화면에 캐시 시각이 나온다.
- 철거 Job은 포털 Trace와 따로 그래프를 메모리에 올린다. WinExe와 같은 코드 경로이고, 한 프로세스에서 작업은 하나만 돈다.
- 브라우저에는 입력 행과 종합 수량만 보낸다. 7개 상세 시트와 Trace 단계는 Excel, HTML에만 있다.
- 노즐 Excel이 DailyReport에 먼저 기록되는 경로는 이번 철거 작업 범위 밖이다.

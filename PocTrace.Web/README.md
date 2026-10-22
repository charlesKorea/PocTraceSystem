# PocTrace.Web

Viewer exe와 **같이** 쓰는 1차 웹 화면입니다. Viewer를 끄지 않습니다.

## 누가 무엇을 하나

| 프로그램 | 어디서 | 하는 일 |
|----------|--------|---------|
| `PocTrace.Admin.exe` | 본인 SVDI, `D:\PocAdmin` | 지금까지처럼 캐시 생성, DailyReport |
| `PocTrace.UI.exe` | 공용 `X:` | 지금까지처럼 각 사용자가 실행 |
| `PocTrace.Web.exe` | 공용 `X:`의 **별도 폴더** | 각 사용자가 실행. 자기 화면의 브라우저만 연다 |

웹은 `127.0.0.1`만 엽니다. 다른 사람 세션에서 이 주소로 들어오지 않습니다. 그래서 Viewer와 같이, **사용자마다 exe를 한 번씩** 실행합니다.

캐시 파일(`X:\jHOOK\PocTraceData`)은 읽기만 합니다. Excel은 공용 드라이브가 아니라 그 사용자 PC의 `%LOCALAPPDATA%\PocTraceWeb\jobs\`에 저장합니다.

## 배포

1. Visual Studio에서 `PocTraceSystem.sln`의 **PocTrace.Web**을 Release로 빌드합니다.
2. `PocTrace.Web\bin\Release\`에서 `PocTrace.Web.exe`, `PocTrace.Web.exe.config`, `wwwroot`, `PocTrace.Core.dll`, `JLic.Core.dll`, `PocTrace.Demolition.exe`, `PocTrace.NozzleTrace.exe`를 예를 들어 `X:\jHOOK\Apps\PocTrace\Web\`에 복사합니다. Newtonsoft, Serilog, EPPlus 같은 NuGet DLL은 exe 안에 들어 있습니다. Viewer 폴더를 덮어쓰지 않습니다. **`Lic\jlab.lic` 는 꼭 같이** 둡니다. AccessApps에 `PT` 또는 `AL` 이 있어야 합니다.
3. Viewer 폴더에 있는 `UtMapping.csv`를 Web 폴더에도 복사합니다. 없으면 Trace는 되지만 UT 매핑이 비어 있습니다.
4. `PocTrace.Web.exe.config`의 `SharedCachePath`, `TargetDatabases`를 Viewer config와 같게 맞춥니다.
5. `AllowDatabaseFallback`은 `false`로 둡니다. DB 비밀번호는 넣지 않습니다.

사용자가 `PocTrace.Web.exe`를 실행하면 콘솔 창이 남고 브라우저가 `http://127.0.0.1:5088/`을 엽니다. 콘솔 창을 닫으면 그 사용자의 웹만 멈춥니다.

포트 5088이 이미 쓰이면 5089부터 차례로 시도합니다.

화면은 DailyReport와 같은 남색 막대와 흰 카드입니다. 오른쪽 위에서 **한국어 / 中文**을 고르면 문구가 바뀌고, 선택은 그 브라우저에 기억됩니다. 글꼴 파일은 SVDI에 이미 있는 Malgun Gothic, Microsoft YaHei를 씁니다.

## 이 버전의 기능

시작 시 `JLic.Core` 로 라이선스를 검사합니다. 실패하면 웹을 열지 않습니다 (종료 코드 2). 권한 `2차` 는 배관 Trace만 되고, 철거·노즐 탭은 숨기고 API는 403입니다.

화면 위 탭 세 개입니다. 한 번에 작업은 하나만 돕니다.

- **배관 Trace:** 라인별 캐시 시각, 48시간 초과 시 거절, 태그 하나, 배치, 설비 요약, Excel
- **철거검수:** Excel 업로드 또는 `Site,공정,건설코드,EQPID` 붙여넣기. 공정과 건설코드는 생략 가능 (`X1,EQP-001`). 검증 그리드에서 잘못된 행은 노란 색으로 남고, 검수는 유효 행만 한다. 진행률, 남은 시간, 행별 추적 상태, 종합 표, Excel, HTML 요약을 보여 준다. 실행 중 취소할 수 있다. 기존 철거 exe와 같은 검수다.
- **노즐추적:** BIM / Instance / GUID. 기존 노즐 exe와 같은 역추적 후 Excel. 파일은 DailyReport 아래에도 남고, 화면에서는 사용자 PC 폴더 복사본을 받습니다

한 번에 철거 입력은 2000행, Excel은 8MB까지다. 종합 표는 화면에서 500행까지 보여주고, 전체는 Excel과 HTML에 있다.

아직 없는 것: 배관 Trace와 노즐추적의 작업 취소, 여러 사람이 캐시 한 벌을 나눠 쓰는 서버.

## 철거검수에서 더 본 것

이번에 웹에 넣은 것은 WinForms에 있고 웹에 빠져 있던 입력·검증·진행·요약·취소다. 아래는 같이 보고 그대로 둔 것이다.

- 철거 검수는 WinForms와 같이 캐시가 48시간을 넘어도 경고만 하고 진행한다. 배관 Trace만 그 시간을 넘으면 거절한다. 검증 화면에 캐시 시각이 나온다.
- 철거 Job은 포털 Trace와 따로 그래프를 메모리에 올린다. WinExe와 같은 코드 경로이고, 한 프로세스에서 작업은 하나만 돈다.
- 브라우저에는 입력 행과 종합 수량만 보낸다. 7개 상세 시트와 Trace 단계는 Excel, HTML에만 있다.
- 노즐 Excel이 DailyReport에 먼저 기록되는 경로는 이번 철거 작업 범위 밖이다.

# PocTrace 웹 포털 API 구상

문서 버전: 2026-10-01  
상태: **1차 구현 시작.** `PocTrace.Web`이 Viewer와 병행한다. 실행 방법은 `PocTrace.Web/README.md`.  
독자: WinForms만 다뤄 본 개발자. HTTP를 처음 보는 수준으로 적었다.

이 문서는 구현 지시서가 아니다. 화면을 웹으로 바꿀 때 **무엇을 재사용하고, 무엇을 새로 두고, 무엇을 브라우저로 보내지 않을지**를 고정한다.

---

## 0. 한 줄 결론

`PocTrace.Core.dll`은 그대로 둔다. HTML이 그 DLL을 프로젝트 참조하듯 집지 않는다. **DLL을 참조하는 작은 C# 웹 프로그램**이 Viewer의 `Program.cs` 자리를 대신하고, 브라우저는 그 프로그램에게 GET/POST 편지로 심부름만 시킨다.

Admin은 지금처럼 **SVDI 본인 계정에서만** 캐시를 만든다. 웹 요청마다 SQL로 그래프를 다시 읽지 않는다.

---

## 1. 목적

C# WinForms(Viewer, 철거, 노즐 역추적)를 웹 화면으로 바꿔, SVDI 브라우저에서 기존 PocTrace 기능을 쓰게 한다.

계산·검수·Excel·캐시 형식은 이미 `PocTrace.Core`에 있다. 바꾸는 것은 껍질이다.

| 유지 | 새로 둠 |
|------|---------|
| Admin의 CacheOnly / Full | 브라우저가 부르는 HTTP API |
| `X:\jHOOK\PocTraceData` 바이너리 캐시 | Core를 참조하는 Windows 서비스 한 프로세스 |
| `X:\jHOOK\DailyReport` 야간 HTML·Excel | 대화형 Trace 화면 (태그 입력, 결과 표, Excel 받기) |
| 검수 규칙, Trace 엔진, Excel 작성 | |

---

## 2. 전제

SVDI는 외부 인터넷이 없다.

- CDN, 클라우드 로그인, 컨테이너 레지스트리, 실행 중 NuGet 복원은 쓰지 않는다.
- 화면용 JavaScript·CSS는 지금 DailyReport처럼 **파일로 같이 배포**한다.
- 개발 PC(인터넷 가능)에서 빌드하고, 결과 폴더만 SVDI로 복사한다. `SVDI_XDrive_Manual_Deploy_KO.md`와 같은 방식이다.
- DB 접속 문자열은 Admin `.exe.config`처럼 **서비스 PC 설정에만** 둔다. 브라우저로 보내지 않는다.

경로 역할은 지금과 같다.

| 위치 | 역할 |
|------|------|
| `D:\PocAdmin\` | Admin 실행, DB 비밀번호. 본인만 |
| `X:\jHOOK\PocTraceData` | 캐시. Admin이 쓰고, 웹 서비스가 읽는다 |
| `X:\jHOOK\DailyReport` | 야간 HTML·Excel. 브라우저는 열람 |

---

## 3. 이미 있는 경계

Viewer도 화면이 Core를 직접 알지 않는다. `PocTrace.UI/Program.cs`가 DLL을 조립하고, 버튼은 `MainViewModel` 메서드만 부른다.

```csharp
var services = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);

var mainViewModel = new MainViewModel(
    services.GraphLoader,
    services.CacheManager,
    services.CreateTraceEngine(),
    services.EngineFactory,
    services.ExcelReport,
    ...);

Application.Run(new MainForm(mainViewModel));
```

버튼과 `RunTraceAsync`는 **같은 PC, 같은 프로세스**라서 프로젝트 참조 하나로 끝난다.

웹 서비스는 이 조립을 그대로 가져온다. `Application.Run(form)` 대신 HTTP 요청을 기다린다.

Viewer가 이미 나눠 둔 동작이 API 후보이다.

| ViewModel | 하는 일 |
|-----------|---------|
| `LoadDataAsync` | 선택한 라인의 캐시를 메모리에 올린다 |
| `SearchAsync` | 태그 하나 Trace |
| `ReTraceSpecificAsync` | 설비 GUID로 다시 Trace |
| `RunBatchTraceAsync` | 태그 여러 개 |
| `ExportToExcelAsync` | 현재 결과를 xlsx로 저장 |

철거(`DemolitionInspectionJob`), 노즐(`NozzleTraceJob`)도 Core를 같은 방식으로 부른다. 포털의 첫 범위는 Viewer 다섯 동작이고, 철거·노즐은 그다음이다.

---

## 4. 목표 그림

```text
[SVDI 브라우저]
        |  사내 HTTP만. 같은 주소에서 화면과 API
        v
[PocTrace.Web.exe  Windows 서비스 1대]
        |  프로세스 안에서 메서드 호출
        v
[PocTrace.Core.dll]
        |  읽기                              야간 배치만 SQL
        v                                    v
X:\jHOOK\PocTraceData                   SQL Server + WEBDB
X:\jHOOK\DailyReport
        ^
        |  CacheOnly / Full
[PocTrace.Admin  본인 SVDI 계정]
```

서비스는 **공용 PC 한 대**가 맞다. 사용자마다 SVDI 안에 서비스를 띄우면 라인 그래프를 사람 수만큼 메모리에 올린다.

역할은 셋으로 유지한다.

| 면 | 하는 일 | 웹으로 바꿀 부분 |
|----|---------|------------------|
| 캐시·야간 산출 | Admin Full / CacheOnly, 48시간 신선도 | 유지. 포털은 신선도만 표시 |
| 대화형 계산 | 태그 Trace, 배치, 철거, 노즐, Excel | 여기만 API |
| 열람 | `progress.html`, `report_*.html`, Full_List | 유지. 포털이 링크하거나 같은 서비스가 파일을 내려 줌 |

야간 HTML은 이미 jQuery, DataTables, ECharts를 로컬 파일로 쓰는 읽기 화면이다. 진행률·검수 요약을 포털에서 다시 만들지 않아도 된다.

---

## 5. HTML은 DLL을 어떻게 부르나

부르지 않는다. 사이에 C# 프로그램이 하나 있다.

```text
[브라우저의 HTML/JavaScript]          [PocTrace.Web.exe]                    [PocTrace.Core.dll]
  버튼처럼 보이는 화면     --글-->      Viewer의 Program.cs 역할      --메서드-->   캐시, Trace, Excel
                           <--글--      프로젝트 참조로 Core를 집음    <--객체--
```

WinForms에 대응하면 이렇다.

| WinForms | 웹 |
|----------|-----|
| `Button.Click` | 브라우저가 GET 또는 POST를 보냄 |
| Click에 연결된 C# 메서드 | URL에 연결해 둔 C# 메서드 |
| 텍스트박스 `.Text` | POST 본문의 `tag` |
| `DataGridView`에 결과 표시 | 서버가 돌려준 글을 HTML 표에 채움 |
| `PocTrace.UI.exe`가 Core를 참조 | `PocTrace.Web.exe`가 Core를 참조 |
| 계산과 화면이 같은 프로세스 | 화면은 브라우저, 계산은 서비스 PC |

브라우저는 C#을 실행하지 않는다. `using PocTrace.Core;`를 HTML에 쓸 수 없다. 브라우저가 아는 것은 주소와 글뿐이다. DLL은 서비스 PC의 exe 옆에 있으며, Viewer 폴더에 `PocTrace.UI.exe`와 `PocTrace.Core.dll`이 같이 있는 것과 같다. 브라우저로 DLL을 내려보내지 않는다.

---

## 6. GET과 POST가 실행되는 방식

둘 다 브라우저가 서버에 보내는 편지이다. 주소 창에 치는 것과, 버튼이 몰래 보내는 것이 같은 종류이다.

**GET**은 조회이다. 서버 데이터를 바꾸지 않는다. 내용이 주소에 붙을 수 있다.

```text
GET /api/lines
```

서버는 캐시 폴더를 보고 “x1은 어제 만들었고, 아직 쓸 수 있다”를 글로 돌려준다. Viewer 시작 시 `CacheFreshnessGuard`가 하는 확인과 같다.

**POST**는 “이 입력으로 일을 해 달라”이다. 입력은 주소가 아니라 편지 본문에 넣는다. 텍스트박스 값을 버튼 클릭 때 넘기는 것과 같다.

```text
POST /api/traces
본문: { "db": "x1", "tag": "EQP-001" }
```

### 6.1 버튼을 한 번 눌렀을 때

1. 사용자가 `EQP-001`을 치고 [Trace]를 누른다.
2. 브라우저의 JavaScript가 POST 편지를 보낸다. 이 화면에는 C# 이벤트 핸들러가 없다.
3. `PocTrace.Web.exe`가 편지를 받는다. 주소가 `/api/traces`이면 미리 연결해 둔 C# 메서드가 실행된다.
4. 그 메서드가 본문에서 `db`, `tag`를 꺼낸다.
5. 메모리에 올려 둔 엔진으로 Viewer와 같은 호출을 한다. `RunTraceAsync("EQP-001", ...)`.
6. Core가 `X:\jHOOK\PocTraceData\GraphCache_x1`을 읽어 Trace 한다. 그래프는 서버 메모리에만 있다.
7. 서버는 그리드에 보일 열만 고른다. `Steps` 전체는 보내지 않는다.
8. 그 목록을 JSON 글로 돌려준다.
9. JavaScript가 그 글을 표로 그린다.

Excel 버튼은 글 대신 파일을 돌려받는다. 서버가 기존 `TraceExcelReportService`로 xlsx를 만든다.

### 6.2 설명용으로 줄인 코드

서버. 참조 관계는 Viewer와 같다.

```csharp
static PocTraceAppServices services;

static void Main()
{
    services = PocTraceAppServices.Create(logger, allowDatabaseFallback: false);
    // Application.Run(form) 대신 HTTP 요청을 기다린다.
}

// GET /api/lines
static string GetLines()
{
    var gate = CacheFreshnessGuard.EvaluateViewerStartup();
    return JsonConvert.SerializeObject(new {
        allowed = gate.Allowed,
        message = gate.Message
    });
}

// POST /api/traces
static async Task<string> PostTrace(string db, string tag)
{
    await LoadLineIfNeeded(db);   // LoadDataAsync 와 같은 역할
    var engine = services.CreateTraceEngine();
    var results = await engine.RunTraceAsync(tag, limitCount: 0, detailedLog: false);

    var rows = results.Select(r => new {
        r.STATUS,
        r.StartNodeTag,
        r.TargetNodeTag
    });
    return JsonConvert.SerializeObject(rows);
}
```

브라우저. DLL 이름은 없다.

```html
<input id="tag" value="EQP-001" />
<button onclick="runTrace()">Trace</button>
<pre id="out"></pre>

<script>
async function runTrace() {
  const tag = document.getElementById("tag").value;

  const res = await fetch("/api/traces", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ db: "x1", tag: tag })
  });

  const rows = await res.json();
  document.getElementById("out").textContent = JSON.stringify(rows, null, 2);
}
</script>
```

`fetch`가 편지를 보내는 함수이다. `method: "POST"`가 편지 종류이고, `body`가 텍스트박스 값이다. `res.json()`이 서버가 돌려준 글을 목록으로 바꾼다.

조회는 본문이 없다.

```javascript
const res = await fetch("/api/lines");  // 기본이 GET
const lines = await res.json();
```

진행 알림은 WebSocket보다 **짧은 간격의 GET 폴링**으로 한다. SVDI 프록시와 오래된 브라우저에서 일반 HTTP가 더 잘 통과한다.

```text
POST /api/batches     → { "jobId": "..." }
GET  /api/batches/그id  → { "percent": 40, "message": "x1 120/300" }
GET  /api/batches/그id/excel  → 파일
```

---

## 7. API 초안

메모리에 남는 것:

- 라인별 `GraphCacheManager` 한 벌. Trace 중에는 읽기 전용. Admin이 캐시를 다시 쓰면 `manifest.bin` 시각이 바뀐 뒤에만 다시 읽는다.
- Trace 결과는 서버에 작업 ID로 잠시 보관하고, 브라우저에는 요약 열만 페이지로 준다.

| 호출 | 의미 |
|------|------|
| `GET /api/lines` | 대상 DB, 캐시 버전, 경과 시간, 48시간 게이트 |
| `POST /api/lines/{db}/load` | 그 라인 캐시를 메모리에 적재 |
| `POST /api/traces` | `{ db, tag, partialMatch, limit }` → 작업 ID |
| `GET /api/traces/{jobId}?page=` | 요약 행 |
| `GET /api/traces/{jobId}/excel` | 서버가 만든 xlsx |
| `POST /api/batches` | 태그 여러 개. 진행률은 폴링 |
| `POST /api/nozzle-traces` | BIM / Instance / GUID. 2단계 |
| `POST /api/demolition/preview` | 붙여넣기 또는 Excel 업로드. 유효/오류 행과 캐시 시각. 검수는 아직 시작하지 않음 |
| `POST /api/demolition` | `{ draftId }` 로 유효 행만 기존 Job 실행. Excel·HTML 요약 |
| `POST /api/jobs/{id}/cancel` | 철거 검수 취소 |

부분 태그 검색과 배치 Trace는 결과가 커질 수 있다. 지금의 `MaxParallelTrace`처럼 서버에 동시 실행 상한을 둔다.

그래프 전체를 JSON으로 주는 API는 만들지 않는다. 한 라인 용량이 크고, `TraceResult.Steps`까지 내리면 메모리와 응답이 같이 무너진다. Admin이 Excel 전에 `ReleaseTraceStepMemory`로 `Steps`를 비우는 이유와 같다.

---

## 8. 호스트

`PocTrace.Core`는 .NET Framework 4.8이다. EPPlus, LargeXlsx, SqlClient, 바이너리 캐시가 이 위에서 이미 동작한다.

1단계 호스트는 **같은 4.8 프로세스에 HTTP를 얹은 Windows 서비스**이다. OWIN 자체 호스트 또는 `HttpListener`. IIS가 없어도 되고, 에어갭 PC에 새 런타임을 설치하지 않아도 된다. 정적 파일(기존 `Templates/assets`)과 API를 **한 주소**에서 제공한다. 브라우저는 `file://`가 아니라 그 주소의 HTML을 연다.

.NET 8로 엔진을 옮기는 일은 포털이 쓰인 뒤로 미룬다. 호스트를 바꾸는 이득보다 검수 결과가 달라질 위험이 크다.

인증은 SVDI 안의 Windows 로그온으로 충분하다. 포털이 사내망에서만 열리면, 서비스가 사용자 이름을 기록하는 수준이면 된다.

---

## 9. 화면

첫 화면은 기존 자산으로 만든다.

- 라인 선택, 태그 입력, 결과 표, Excel 받기
- `DailyReport`의 progress / report 로 가는 링크

DataTables로 결과 표를 그리면 새 프론트 도구 없이도 Viewer 그리드와 같은 열을 보여줄 수 있다.

화면을 더 만들 때는 인터넷 되는 PC에서 빌드하고 **빌드 결과만** SVDI로 복사한다. 실행 중에 npm이나 CDN을 치지 않는다.

WinForms Viewer는 서비스가 같은 Trace와 같은 Excel을 낼 때까지 나란히 둔다.

---

## 10. 구현 가능성

| 항목 | 판단 |
|------|------|
| Trace·검수·Excel·캐시 재사용 | 가능. Core는 이미 WinForms를 참조하지 않음 |
| Viewer 기능을 API로 옮기기 | 가능. ViewModel 메서드가 곧 주소 |
| 야간 HTML을 포털 안으로 다시 작성 | 하지 않아도 됨. 링크면 충분 |
| 여러 사용자 | 가능. 캐시는 공유 읽기, 작업 결과만 사용자별로 분리 |
| 그래프를 JSON API로 제공 | 하지 않음 |

리스크는 세 가지이다.

- **메모리.** 라인을 전부 상주시키면 Admin이 DB 사이에 메모리를 비우는 이유와 같은 문제가 난다. 자주 쓰는 라인만 올리고 나머지는 버린다.
- **캐시 교체.** Admin이 `nodes.bin` 등을 쓰는 동안 서비스가 읽으면 깨진 세대를 올릴 수 있다. 교체는 폴더 단위로 끝내고, 서비스는 매니페스트가 바뀐 뒤에만 다시 읽는다.
- **긴 작업.** 철거·배치·Full List는 HTTP 요청 안에서 끝나면 안 된다. 작업 ID, 진행률, 완료 파일, 디스크 보관 기간이 필요하다.

엔진을 다른 언어로 다시 짜지 않는다. 검수 규칙이 C#에 있다. WinForms 창을 서버에서 원격으로 누르는 방식도 쓰지 않는다. 파일 대화상자와 UI 스레드가 API에 남기 때문이다.

---

## 11. 만드는 순서

1. **Viewer 다섯 동작만** 서비스로 옮긴다. 라인 로드, 단건 Trace, 재Trace, 배치, Excel.
2. 결과 열과 Excel이 기존 Viewer와 같으면 WinForms Viewer를 포털로 교체한다.
3. 철거, 노즐 역추적을 같은 작업 ID 방식으로 붙인다.
4. 야간 Admin과 DailyReport는 그 사이에도 그대로 돈다.

---

## 12. 1차 구현 (PocTrace.Web)

구상 중 대화형 Viewer만 코드로 옮겨 두었다. Admin, DailyReport, Viewer exe는 그대로다.

- 프로젝트: `PocTraceSystem/PocTrace.Web`
- 각 SVDI 사용자가 공용 X:의 exe를 실행하면, 그 세션의 `127.0.0.1`에서만 브라우저가 열린다. 다른 사용자와 프로세스를 공유하지 않는다. Viewer를 여러 사람이 각자 실행하는 방식과 같다.
- 캐시는 읽기 전용. Excel은 `%LOCALAPPDATA%\PocTraceWeb`에 저장해 공용 폴더를 서로 덮어쓰지 않는다.
- 기능: 캐시 신선도, 태그 하나 Trace, 배치 Trace, 요약 표, Excel, 노즐추적, 철거검수(Excel 업로드, 검증 그리드, 진행률, 취소, HTML 요약).
- 실행과 복사 위치는 `PocTrace.Web/README.md`.

## 13. 운영 원칙 (확정)

1. Admin은 SVDI 본인 계정에서만 캐시를 만들고 DailyReport를 쓴다.
2. 포털 요청은 SQL로 그래프를 다시 만들지 않는다. `AllowDatabaseFallback`은 Viewer와 같이 끈다.
3. 대화형 계산은 `PocTrace.Web.exe`가 한다. 당분간은 Viewer와 같이 사용자마다 실행하고, 그 세션의 `127.0.0.1`만 연다.
4. 열람용 야간 HTML은 `X:\jHOOK\DailyReport`에 둔다. 포털은 그 입구이다.
5. 브라우저로 보내는 것은 요약 행, 진행률, 파일이다. 그래프와 `Steps` 전체는 서버에 남긴다.

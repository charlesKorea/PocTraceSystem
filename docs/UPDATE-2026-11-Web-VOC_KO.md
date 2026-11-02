# 2026-11 Web VOC 11건 반영

대상: `PocTrace.Web.exe` (Core, Admin, Viewer, 철거, 노즐 exe는 코드 변경 없음)
실행 위치와 DLL 규칙은 `docs/SVDI-SHARED-RUN.md` 그대로다.

## 한눈에 보기

| # | VOC | 반영 | 주요 파일 |
|---|-----|------|-----------|
| 1 | 캐시 불러오기 10% → 100% 점프 | Core가 보내는 단계 문구를 진행률로 바꿈 (파일 읽기 4~66%, 그래프 준비 70~94%). 한 단계가 길면 화면이 천천히 움직임 | `CacheLoadStages.cs`, `PortalService.EnsureLoaded/SetStage`, `index.html` (`anim*`) |
| 2 | 기능별 사용법 팝업 | 상단 라이트/다크 오른쪽 `?` 버튼. 시작하기, 배관 Trace, 배치·Excel, 철거검수, 노즐추적, 검수 기록, 문제 해결. 한국어/中文 | `index.html` (`HELP`, `openHelp`), `circle-help.svg` |
| 3 | 철거검수 진행 표시 | 그라데이션 원형 진행 표시. Line, EQPID 진행 수, 남은 시간(ETA), 취소 버튼. 철거 탭은 상단 라인 영역이 숨겨져 진행이 안 보였던 것도 해결 | `index.html` (`#jobCard`) |
| 4 | 중문 업종 용어 영어 유지 | Nozzle, Duct, Damper, Excel, ID, Instance, Tag, Trace, Key, POC, EQPID | `index.html`, `history.html`, `PortalService.cs`, `PortalServer.cs` |
| 5 | 라인 옆 DB sync 날짜 | `x1  (DB sync 2026-11-02 11:30)`. 값은 캐시 manifest의 저장 시각 = Admin이 DB에서 캐시를 만든 시각 | `PortalService.GetLines`, `index.html` (`lineLabel`) |
| 6 | 예상 메모리 상단 표시 | 상단 칩. 올리기 전: 예상 메모리, 전체 RAM 대비 %, 가용 메모리. 올린 뒤: 이 프로세스 사용량 | `SystemMemory.cs`, `PortalService.GetLines` |
| 7 | 배관 Trace Excel 배치 | 배치 카드의 **Excel에서 불러오기**. A열 2행부터 EQPID, 1행 제외, 빈 칸·중복 제거 | `WebExcel.ReadColumnA`, `POST /api/batches/preview` |
| 8 | 검수 기록 공용화 | 개인 로컬 + 공용 폴더에 기록. 조회 조건(사용자, 방식, 라인, 기간, 대상)과 Excel 다운로드 | `InspectionHistory.cs`, `history.html`, `GET /api/history`, `/api/history/excel` |
| 9 | 철거검수 표가 머리글을 벗어남 | 원인 재현 후 수정. 규칙은 `docs/Web_Table_Layout_Guide_KO.md` | `jcs-glass.css`, `index.html` |
| 10 | 배관 Trace 진행 UI | 원형 진행 표시에 캐시 → Trace → Excel 단계, 배치는 Tag n/m, 현재 Tag, 경과 시간 | `PortalService.RunSingle/RunBatch/SetPhase` |
| 11 | 변경 파일만 zip | `updates/` 폴더. 만드는 스크립트 `tools/Make-UpdateZip.ps1` | `updates/README.md` |

## 설정 키 (`PocTrace.Web.exe.config`)

| 키 | 기본값 | 설명 |
|----|--------|------|
| `SharedHistoryPath` | `Y:\sds\999. data` | 모든 사용자의 검수 기록을 모으는 공용 폴더. 이 아래 `PocTraceWebHistory`에 쌓는다 |
| `MemoryEstimateFactor` | `3.0` | 예상 메모리 = 캐시 폴더 크기 × 이 값 + 300MB. 실제 `memory-load.log`와 비교해 조정한다 |

## 공용 검수 기록 구조

```
Y:\sds\999. data\PocTraceWebHistory\
  202611_hong.jsonl      월_사용자.jsonl, 한 줄에 검수 1건 (JSON)
  202611_kim.jsonl
```

- 사용자·월마다 파일을 나눠 여러 PC가 같은 파일을 동시에 열 일을 줄였다. 쓸 때는 `FileShare.ReadWrite`로 한 줄을 붙이고, 막히면 4번까지 다시 시도한다.
- 한 줄: `{"At":"2026-11-02T03:12:44Z","UserName":"hong","Method":"batch","Line":"x1","Targets":["EQP-1","EQP-2"],"Machine":"PC-01"}` (시각은 UTC, 화면에서 이 PC 시각으로 바꿔 보여 준다)
- 개인 로컬 기록(`%LocalAppData%\PocTrace\Web\inspection-history.json`)은 그대로 남는다. 조회할 때는 로컬과 공용을 합쳐 같은 기록을 한 번만 센다.
- 공용 폴더가 없거나 3초 안에 응답이 없으면 로컬 기록만 쓰고, 검수 기록 화면 위에 안내가 나온다. 이때도 검수는 정상이다.
- 공용 폴더 쓰기 권한이 필요하다. 읽기만 되면 기록은 로컬에만 남는다.
- 조회는 최대 5000행까지 보여 주고, 넘으면 조건을 좁히라고 안내한다. Excel 다운로드는 현재 조회 조건 그대로다.

## 진행률이 움직이는 방식

서버는 단계가 바뀔 때만 값을 올린다 (단조 증가). 화면은 그 값까지 부드럽게 따라가고, 단계가 오래 걸리면 다음 단계 직전(`creepTo`)까지 천천히 올라간다. 100%는 실제로 끝났을 때만 나온다.

| 작업 | 구간 |
|------|------|
| 캐시 불러오기 | 0~100 (위 단계 표) |
| 단건 Trace | 캐시 0~50, Trace 50~86, Excel 88~98 |
| 배치 Trace | 캐시 0~20, Tag별 20~92, 요약 Excel 93~98 |
| 철거검수 | 철거 Job이 보내는 값 그대로 + 화면 보간 |

정확한 시간 예측은 아니다. 사용자가 멈춘 것으로 느끼지 않게 하는 용도다.

## 빌드할 때 달라진 것

- 새 소스 4개: `CacheLoadStages.cs`, `InspectionHistory.cs`, `SystemMemory.cs`, `WebExcel.cs` (`PocTrace.Web.csproj`의 Compile 목록에 추가됨)
- Web 프로젝트에 `EPPlus 7.2.1` PackageReference와 `System.Drawing` 참조가 추가되었다. EPPlus는 Costura로 `PocTrace.Web.exe` 안에 들어간다. 폴더에 `EPPlus.dll`이 남아 있으면 지운다.
- `PocTrace.Web.exe.config`에 위 키 두 개가 늘었다. 기존 config를 쓰고 있다면 키를 추가하거나 새 파일로 바꾼다.
- `wwwroot\assets\jcs-ui\icons`에 `circle-help.svg`, `cpu.svg`, `upload.svg`가 늘었다.

## 중문 용어 정책

영어로 남기는 말: Nozzle, Duct, Damper, Excel, ID, BIM ID, Instance, GUID, Tag, Trace, Key, POC, EQPID, Takeoff, Hookup, Site.
`缓存`, `线路`, `设备`처럼 일반 단어는 한자를 그대로 쓴다.
새 문구를 중문으로 쓸 때 위 영어 단어는 한자로 번역하지 않고, 영어 단어와 한자 사이에는 공백을 둔다 (`请输入 Tag。`).

# 변경 파일 목록 - 2026-11-02_web-voc2

- 기준: `dc4371b` 이후 ~ `023e948`
- 파일 수: 17

| 구분 | 상태 | 파일 | 마지막 커밋(UTC+8) |
|---|---|---|---|
| wwwroot (재빌드 불필요, 폴더 덮어쓰기) | 수정 | `PocTrace.Web/wwwroot/assets/jcs-ui/jcs-glass.css` | 2026-11-02 16:08 |
| wwwroot (재빌드 불필요, 폴더 덮어쓰기) | 추가 | `PocTrace.Web/wwwroot/assets/jcs-ui/poc-keepalive.js` | 2026-11-02 16:08 |
| wwwroot (재빌드 불필요, 폴더 덮어쓰기) | 수정 | `PocTrace.Web/wwwroot/history.html` | 2026-11-02 16:08 |
| wwwroot (재빌드 불필요, 폴더 덮어쓰기) | 수정 | `PocTrace.Web/wwwroot/index.html` | 2026-11-02 16:08 |
| 문서 | 수정 | `PocTrace.Web/README.md` | 2026-11-02 16:08 |
| 문서 | 수정 | `docs/README.md` | 2026-11-02 16:08 |
| 문서 | 추가 | `docs/UPDATE-2026-11-Web-VOC2_KO.md` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/App.config` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 추가 | `PocTrace.Web/BrowserWatch.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/InspectionHistory.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 추가 | `PocTrace.Web/PathMask.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/PocTrace.Web.csproj` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/PortalServer.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/PortalService.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/Program.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 추가 | `PocTrace.Web/SingleInstance.cs` | 2026-11-02 16:08 |
| 소스 (빌드 필요) | 수정 | `PocTrace.Web/WebLocalStore.cs` | 2026-11-02 16:08 |

## 적용 방법

1. zip을 공유 폴더 작업 위치에 풀어 같은 경로의 파일을 덮어쓴다 (저장소 루트 기준 상대 경로).
2. `소스` 구분 파일이 있으므로 `PocTrace.Web`을 다시 빌드해 Web.exe를 공유 폴더에 배포한다 (Core.dll은 변경 없음).
3. `PocTrace.Web/wwwroot`는 exe와 같은 폴더의 `wwwroot`에 덮어쓴다.
4. `Web.exe.config`에 `AutoExitOnBrowserClose`, `AutoExitGraceSeconds`, `AutoExitStaleSeconds` 키를 추가한다 (`PocTrace.Web/App.config` 참고). 앞선 업데이트의 `SharedHistoryPath`, `MemoryEstimateFactor`도 없으면 추가한다.
5. 폴더에 남은 Serilog/EPPlus/Dapper/JLic.Core.dll이 있으면 지운다 (Costura 내장).

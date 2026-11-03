# 업데이트 2026-11-03_report-progress

- 기준: `cd51769` 이후 `8cf82b1` 까지
- 파일 수: 변경 18개, 삭제 0개
- 시각은 중국 표준시(UTC+8) 기준 마지막 커밋 시각입니다.
- 자세한 내용: `docs/UPDATE-2026-11-Report-Progress_KO.md`

## 적용 순서

1. zip을 저장소 루트 기준으로 덮어쓴다.
2. **PocTrace.Core -> PocTrace.Admin -> PocTrace.Web** 순서로 Release 빌드한다. (새 .cs 파일은 csproj에 이미 포함)
3. `D:\PocAdmin`에 Admin 새 빌드와 Templates를, 공유 폴더에 `PocTrace.Core.dll`(같은 빌드)과 `PocTrace.Web.exe`, `wwwroot\index.html`을 배포한다.
4. Admin Full을 한 번 실행해 HTML 데이터를 다시 만든다.
5. 폴더에 남은 Serilog/EPPlus/Dapper/JLic.Core.dll은 지운다 (Costura 내장).

## 바뀐 파일

| 파일 | 상태 | 구분 | 마지막 수정 |
|------|------|------|-------------|
| `PocTrace.Admin/Jobs/DailyTraceJob.cs` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Core/Engine/Progress/ASSUMPTIONS.md` | 수정 | 문서 | 2026-11-03 11:07 |
| `PocTrace.Core/Engine/Progress/DesignFootprintBuilder.cs` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Core/Engine/Progress/DesignProgressEngine.cs` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Core/Engine/Progress/PipeDesignDateSource.cs` | 추가 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Core/PocTrace.Core.csproj` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/css/style.css` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/js/app.js` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/js/lang.js` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/js/poc-charts.js` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/js/poc-grid.js` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Core/Templates/assets/js/poc-progress.js` | 수정 | Templates (Core 빌드 시 포함, Admin의 Templates 폴더에 덮어쓰기) | 2026-11-03 11:07 |
| `PocTrace.Web/PocTrace.Web.csproj` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Web/PortalServer.cs` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Web/PortalService.cs` | 수정 | 소스 (빌드 필요) | 2026-11-03 11:07 |
| `PocTrace.Web/wwwroot/index.html` | 수정 | wwwroot (exe 옆 wwwroot에 덮어쓰기) | 2026-11-03 11:07 |
| `docs/HTML_Progress_Summary_VOC_KO.md` | 수정 | 문서 | 2026-11-03 11:07 |
| `docs/UPDATE-2026-11-Report-Progress_KO.md` | 추가 | 문서 | 2026-11-03 11:07 |

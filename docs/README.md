# PocTrace 문서 안내 / 文档说明

| 파일 / 文件 | 언어 | 용도 |
|-------------|------|------|
| `PocTrace_Validation_Catalog_KO.csv` | 한국어 | 검수 항목 → **Excel에서 바로 열기** |
| `PocTrace_Validation_Catalog_ZH.csv` | 中文 | 检核项目 → **用Excel直接打开** |
| `PocTrace_Validation_Catalog_KO.md` | 한국어 | 표 복사→Excel 붙여넣기용 |
| `PocTrace_Validation_Catalog_ZH.md` | 中文 | 表格复制→Excel |
| `PocTrace_Special_Trace_Rules_KO.md` / `.csv` | 한국어 | **Water/MainLine Takeoff 등 특수 Trace 로직** |
| `PocTrace_Special_Trace_Rules_ZH.md` / `.csv` | 中文 | **Water/MainLine Takeoff 等特殊 Trace 逻辑** |
| `PocTrace_User_Manual_KO.md` | 한국어 | 사용자 매뉴얼 |
| `PocTrace_User_Manual_ZH.md` | 中文 | 用户手册 |
| `PocTrace_Validation_and_Manual.xlsx` | 한·중 | 검수 + 특수규칙 시트 통합 |
| `SVDI-SHARED-RUN.md` | 한국어 | **SVDI 실행 기준**. Admin은 D:, UI·철거·노즐·Web은 같은 폴더와 같은 Core.dll. 코딩할 때 이 문서를 따른다 |
| `UPDATE-2026-10.md` | 한국어 | **2026-10 변경 요약** (캐시 로드 메모리, Web 포털, JLic 검증, 배포) |
| `SVDI_XDrive_Manual_Deploy_KO.md` | 한국어 | **SVDI X: 수동 배포·Admin 영향** |
| `WebPortal_API_Concept_KO.md` | 한국어 | **웹 포털 API 구상** (WinForms → HTML, Core 재사용, GET/POST) |
| `PocTrace.Web/README.md` | 한국어 | **PocTrace.Web 실행** (Viewer와 병행, X: 배포, UiAssets 파일 목록) |
| `UPDATE-2026-11-Web-VOC2_KO.md` | 한국어 | Web.exe 추가 VOC 7건(기록 삭제, 경로 비노출, 탭 초기화, 중복 실행 방지 등) |
| `UPDATE-2026-11-Web-VOC_KO.md` | 한국어 | **2026-11 Web VOC 11건 반영 내역**, 설정 키, 공용 검수 기록 구조 |
| `Web_Table_Layout_Guide_KO.md` | 한국어 | **Web 표가 머리글을 벗어나는 문제**의 원인과 표 작성 규칙 |
| `../tools/Make-UpdateZip.ps1` | PowerShell | **변경 파일만 zip**. `updates/`의 과거 zip은 적용이 끝나 2026-11-12에 삭제했다. 다음에 필요하면 이 스크립트가 `updates/`를 다시 만든다 |
| `PocTrace.DeletedPipeLength/가이드.md` | 한국어 | **삭제 배관 길이 Excel** 열 뜻, 순서 열, 탐색 상한 |
| `BinaryCache_Easy_Guide_KO.md` | 한국어 | **공유 캐시 쉬운 설명**（비개발자·동료 공유） |
| `BinaryCache_Easy_Guide_ZH.md` | 中文 | **共享缓存简明说明**（非开发同事共享） |
| `DDW_Cache_Strategy_KO.md` | 한국어 | **DDW DB로 같은 공유 캐시를 만드는 전략**. S5D 캐시는 유지하고 폴더 키를 분리한다. 12절이 SVDI 조회 순서 |
| `ddw-svdi-queries/` | SQL | **remote_monami 조회 팩**. `DDW-SVDI-Queries.zip`과 같은 내용 |
| `DDW_Trace_Flow_KO.md` | 한국어 | **DDW에서 설비 Tag가 배관 길이까지 이어지는 추적 flow**(담당자 설명 2026-11-12), S5D·현재 코드와의 차이, 질문 Q-01~Q-12와 담당자 답변, SVDI 시험 도구 `PocTrace.DdwTraceBench` 설명 |
| `DDW_SVDI_Results_KO.md` | 한국어 | **SVDI에서 받은 DDW 조회 결과와 결정 기록**. 확정 사실, 진행 표, 코드 영향, 열린 질문을 쌓는다 |
| `WORKFLOW_KO.md` | 한국어 | **작업 반영 방식**. 별도 요청이 없는 한 코드·문서를 고치면 main에 반영한다(2026-11-12 지시) |

CSV는 UTF-8 BOM이라 Excel에서 한글/중국어가 깨지지 않습니다.

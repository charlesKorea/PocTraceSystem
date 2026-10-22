# Design Progress / Summary HTML VOC 정리 (2026-08)

## Summary (`index.html`) — UX v4

| VOC | 반영 |
|-----|------|
| 라인 탭(X1, X2-PH1…)이 Check Rules보다 먼저 | `poc-core.js` `rebuildLineNav` |
| QP 점수 좌측 + 우측 텍스트 없는 추세 곡선 | `poc-core.js` `renderQpSparkline` |
| 첫 화면 QP·FAIL 중심, 차트 접기 | `app.js` + `ui-collapse-panel` |
| KPI 가로 스크롤(모바일) | `style.css` `kpi-scroll-row` |
| Apple 톤 색·여백·포커스 링 | `style.css` V4 |

## Progress (`progress.html`)

| # | VOC | 반영 |
|---|-----|------|
| 1 | 저신뢰 표본 의미·인수·예시 | 페이지 `details` VOC 패널 + KPI 툴팁 |
| 2 | 추이 그래프 숫자 표시 | echarts `label.show: true` |
| 3 | DataTables(sort/search/excel/20페이지) | `poc-progress.js` DataTable + buttons.html5 |
| 4 | BOM% 근거 · UNKNOWN | Fitting→직전 Pipe UT 귀속(`DesignFootprintBuilder`); 기준선 0이면 `—` |
| 5 | Utility 버튼 삭제, Tag 클릭 펼침 + 복귀 | Tag 링크 → 패널 스크롤, `표로 돌아가기` |
| 6 | N 의미 | VOC 패널 + 열 툴팁 (`CohortN`) |
| 7 | No · 공정 열 | `_rowNo`, `Process` |
| 8 | 최근설계일 · `-` · 기본 내림차순 | Pipe OpTime max; 없으면 `-`; `order: LastDesign desc` |
| 9 | 설계시작일 · 설계진행일 | Pipe OpTime min; `DesignDays = Last − First` |
| 10 | UX v4: 설계기간 한 열 · 진행% 미니바 · sticky 표 | `poc-progress.js` + `style.css` |

## SVDI 업데이트 파일

| 로컬 | → 개인 Admin | → 공용 DailyReport |
|------|--------------|-------------------|
| `Templates/layout.html` | `D:\PocAdmin\Templates\` | `X:\jHOOK\DailyReport\` |
| `Templates/assets/js/poc-core.js` | 동일 | `assets/js/` |
| `Templates/assets/js/app.js` | 동일 | 동일 |
| `Templates/assets/js/poc-progress.js` | 동일 | 동일 (휘발성 목록에 포함됨) |
| `Templates/assets/js/lang.js` | 동일 | 동일 |
| `Templates/assets/css/style.css` | 동일 | 동일 |
| `PocTrace.Core.dll` (BOM UT 귀속) | `D:\PocAdmin\` | — |

Admin **Full** 후 Progress 데이터 재생성 필요(BOM UT 수정 반영).

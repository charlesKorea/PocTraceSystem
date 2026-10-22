# M-Link / PocTrace 사용자 매뉴얼 (한국어)

문서 버전: 2026-07 · 대상: Viewer(대화형) · Admin(일배치) · HTML 리포트

---

## 1. 시스템 개요

| 구성 | 역할 |
|------|------|
| **PocTrace.UI (Viewer)** | 캐시 로드 후 설비 Tag 검색 → Trace → 그리드/Excel |
| **PocTrace.Admin** | DB→BinaryCache→전 설비 Trace→검수→HTML/Excel 일배치 |
| **HTML DailyReport** | index / report_{라인} / rules / progress — 공유폴더에서 브라우저로 열람 |
| **BinaryCache v5** | 라인별 GraphCache_{db}/ (nodes,pocs,pipes,takeoffs,dynprops) |

연결 정합성(검수)과 설계 진행률(Progress)은 **별도 KPI**입니다.

---

## 2. Viewer 사용 순서

1. `PocTrace.UI.exe` 실행 (같은 폴더의 `PocTrace.UI.exe.config` 확인)
2. **라인(DB)** 선택 후 캐시 로드  
   - 캐시가 `CacheMaxAgeHours`(기본 48h)보다 오래되면 시작 차단 → Admin으로 캐시 갱신
3. 설비 **Tag** 입력 후 검색 (부분매칭·Scrubber 콤마 Tag 지원)
4. 결과 그리드에서 PASS/FAIL·이슈·유사모델(MainName) 확인
5. Excel 내보내기로 상세 경로·이슈 저장

### 주요 설정 (`PocTrace.UI.exe.config`)

| 키 | 설명 |
|----|------|
| SharedCachePath | BinaryCache 루트 |
| TargetDatabases | x1,x2,x2_2 … |
| CacheMaxAgeHours | Viewer 하드 게이트(시간) |
| EnableDetailedDebug | true 시 Trace 상세 로그 |
| MaxParallelTrace | 병렬도 |

---

## 3. Admin 일배치

1. `PocTrace.Admin.exe` 실행 · `PocTrace.Admin.exe.config`의 SourceDb/PocTraceDB·경로 확인
2. **Full**: DB조회 → 캐시저장 → 전설비 Trace+검수 → HTML/Excel → Rules/Progress  
   **CacheOnly**: 캐시만 갱신 (Viewer 48h 게이트용)
3. 산출물: `DailyReportPath`  
   - `index.html`, `report_{db}.html`, `rules.html`, `progress.html`  
   - `data_*.js`, Excel 폴더, History JSON

| 키 | 설명 |
|----|------|
| DailyReportPath | HTML/Excel 출력 |
| DesignProgressEnabled | 설계 진행률 리포트 on/off |
| FrmConnectionString | 비어 있으면 Graph-only Progress |
| EnableDetailedDebug | 배치 상세 로그 |

---

## 4. HTML 리포트 보는 법

브라우저로 `DailyReportPath` 또는 미리보기 폴더의 HTML을 엽니다. **CDN 없음 · 로컬 assets만**.

| 페이지 | 내용 |
|--------|------|
| index.html | 라인 통합 Summary · Quality Point |
| report_{db}.html | 설비 KPI · 차트 · Top 이슈 조치 · 그리드 |
| rules.html | Check Rules 목록 · 일별 Hit 추이 |
| progress.html | 설계 진행률(MainName 코호트 대비 Length/BOM %) |

언어: 우측 상단 ko / en / zh.

---

## 5. 검수 결과 해석

- **PASS**: 해당 POC 경로에 IssueList 없음  
- **FAIL**: 하나 이상 검수 이슈 (여러 Rule 동시 가능)  
- 이슈 상세·귀속: Excel Error 시트 / HTML 조치 Top / Belong 차트  
- 검수 항목 전체: `PocTrace_Validation_Catalog_KO.csv` (또는 .md)

STATUS: EQP / NOZ / TOF / CAP / EMP / ING / ERR — 카탈로그 문서 참고.

---

## 6. Design Progress (설계 진행률)

- **진행중**: 배관(Pipe) OpTime이 오늘 기준 D-2 이내  
- **진행률%**: 유사 MainName 완료 설비의 Length·BOM **중앙값** 대비 (기본 가중 0.6/0.4)  
- 표본 부족 시 Low Confidence 표시  
- Utility 버튼으로 UT별 분해

---

## 7. 문제 해결

| 증상 | 조치 |
|------|------|
| Viewer 캐시 너무 오래됨 | Admin CacheOnly 또는 Full |
| Detail 로그 안 나옴 | exe와 같은 폴더 config의 EnableDetailedDebug=true 후 재실행 |
| HTML 깨짐/차트 없음 | assets 폴더 동봉·오프라인 경로 유지 |
| Progress 0%·저신뢰 | MainName 동일 완료 설비 부족 또는 FRM 미연결 |

---

## 8. 관련 문서

- 검수 카탈로그: `PocTrace_Validation_Catalog_KO.csv` / `.md`
- **특수 Trace(Water/MainLine Takeoff 등)**: `PocTrace_Special_Trace_Rules_KO.md` / `.csv`
- 중국어: `PocTrace_User_Manual_ZH.md`, `PocTrace_Validation_Catalog_ZH.*`, `PocTrace_Special_Trace_Rules_ZH.*`

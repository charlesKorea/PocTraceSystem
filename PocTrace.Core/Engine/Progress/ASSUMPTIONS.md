# Design Progress — locked assumptions (plan §9)

현장/고객 확정 전까지 아래 기본값을 코드에 고정한다. 변경 시 AppSettings 또는 본 문서·옵션을 갱신.

| Topic | Locked default |
|-------|----------------|
| FRM access | `FrmConnectionString` 비어 있으면 Graph-only. 설정 시 `FrmEquipmentView` 조회, 실패 시 빈 마스터 |
| Design date | Trace Pipe steps only (설비/노즐 node 제외). S5D `PipeLog`(Origin=0, OperationType<2)에서 파이프별 **Length가 바뀐 행**만 변경으로 본다(최초 기록은 변경으로 인정, 허용오차 0.05mm). **Last** = 설비 내 파이프들의 마지막 Length 변경일, **First(설계시작일)** = 최초 기록일. PipeLog 조회 불가/미연결 시 `Pipe.OpTime`(max/min)으로 대체.  **DesignDays** = Last.Date − First.Date (일, 동일일=0) |
| Cohort key | `MainName` = Node.Name (`StartNodeName`), FRM `ModelKey` fallback |
| Completed cohort | not InProgress (D-InProgressDays) AND LastDesign ≤ Today-IdleStableDays (14), OR FRM OfficialStatus contains Complete |
| BOM | Fitting (+ Damper) distinct `NodeId|QCode` count along Trace footprint. Utility drill-down: fittings inherit last Pipe Utility on the same path (not all UNKNOWN) |
| Equipment pipe scope | Union of pipes across all POC Trace paths for the Tag |
| Shared MainLine pipes | May be counted in multiple Tags (known limitation; no split yet) |
| D-2 calendar | Calendar days, KST machine local `DateTime.Today`, weekends included |
| Report scope | All equipment with Trace results; per-DB + integrated progress.html |
| Delivery | DailyReportPath shared HTML (same as connection integrity) |
| % formula | LengthPct/BomPct capped 100; Progress = 0.6*Length + 0.4*Bom; Overrun badge if current > baseline |

Phase B view expected columns: `Tag`, `ModelKey`, `PlannedLengthMm`, `PlannedBomCount`, `OfficialStatus`, `DesignMilestoneDate`.

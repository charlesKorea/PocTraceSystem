# Design Progress — locked assumptions (plan §9)

현장/고객 확정 전까지 아래 기본값을 코드에 고정한다. 변경 시 AppSettings 또는 본 문서·옵션을 갱신.

| Topic | Locked default |
|-------|----------------|
| FRM access | `FrmConnectionString` 비어 있으면 Graph-only. 설정 시 `FrmEquipmentView` 조회, 실패 시 빈 마스터 |
| Design date | `PipeLog.OPTIME` via Trace Pipe steps only (설비/노즐 node 제외). **Last** = max OpTime, **First(설계시작일)** = min OpTime, **DesignDays** = Last.Date − First.Date (일, 동일일=0) |
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

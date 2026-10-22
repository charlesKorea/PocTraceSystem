# PocTrace 특수 Trace / Takeoff 로직 설명 (한국어)

> Excel: 같은 폴더 `PocTrace_Special_Trace_Rules_KO.csv`를 열거나, 아래 표를 복사→붙여넣기  
> 관련: 검수 카탈로그 `PocTrace_Validation_Catalog_KO.*` · 구현 `TraceAlgorithm.WaterTakeoff.cs` / `TraceAlgorithm.MainLineTakeoff.cs`

문서 버전: 2026-07

---

## 1. 한눈에 보기 — Takeoff 도달 시 분기

Outlet이 Takeoff(Damper)이면 **아래 순서**로 처리합니다 (`TraceAlgorithm`).

| 우선순위 | 특례 이름 | 조건(시작 POC/설비) | 동작 | STATUS 결과 경향 |
|----------|-----------|---------------------|------|------------------|
| 1 | MainLine Takeoff (VOC-1) | SubDivision1에 **MAIN** + UtilityName에 **vent / vac / fore line·fore-line·foreline** | TOF로 **종료하지 않음**. Takeoff를 Step에 넣고 하류 continuation을 BFS 큐에 등록. **형제 큐 폐기**. 종료 후 EQP가 있으면 **최단 EQP 1건만** 남김 | 정상: **EQP**(TOF 경유). 실패: ING 등 |
| 2 | Water Takeoff | UtilityGroupName에 **water** (대소문자 무시) | TOF로 **종료하지 않음**. continuation을 큐에 등록(형제 큐는 유지). 기존 BFS로 NOZ/CAP/EQP 계속 | 하류 종단 STATUS |
| 3 | 일반 Takeoff | 위 둘 아님 | Takeoff에서 **TOF로 종료**. Tag 관련 여부는 `EquipmentTagMatcher` | **TOF** |

```
POC → Pipe → … → Takeoff?
              ├─ MainLine? → continuation BFS → (Prefer 최단 EQP)
              ├─ Water?    → continuation BFS
              └─ else      → STATUS=TOF 종료
```

---

## 2. 특례 상세 표 (복사→Excel)

| No | 특례ID | 구분 | 적용 조건 | 엔진 동작(상세) | 결과/부작용 | 검수 연계 | 구현 파일 |
|----|--------|------|-----------|-----------------|-------------|-----------|-----------|
| 1 | SP-WATER-TOF | Trace 엔진 | 시작 POC `UtilityGroupName`에 "water" 포함 | Takeoff Step(`Damper[Tag]`, NodeType=Takeoff) 추가 후, `TryResolveTakeoffContinuationPoint`로 하류 Outlet을 구해 큐 Enqueue. 재방문 Takeoff는 역류로 **소멸(ING 미기록)**. continuation 없으면 **ING** (`Water Takeoff[…] - No connected pipe`) | 일반 TOF 종료 억제. Water는 형제 분기 큐를 비우지 않음 | TOF로 남으면 연결정보상이 Feedback에 `{UtilityGroup} Takeoff` 문구 가능. Water는 보통 TOF가 아닌 하류 STATUS | `TraceAlgorithm.WaterTakeoff.cs` |
| 2 | SP-MAINLINE-TOF | Trace 엔진 (VOC-1) | Owner Sub1에 "MAIN" + UtilityName이 vent/vac/fore line/fore-line/foreline 중 하나 **포함** | Water와 동일 continuation + BFS. **차이**: TOF 도달 시 `queue.Clear()`로 형제 분기 폐기 후 continuation만 진행. Trace 종료 시 `PreferMainLineEqpResult`: EQP 있으면 Length→Hop 순 **최단 EQP 1건**, 나머지 폐기. EQP 없으면 하류 결과 유지 | 연속 Takeoff에서도 자체 루프 ING 오탐 방지(구버전 버그 수정). 단일 최단 EQP 전제 | `MainLineTraceHelper.IsMainLineTofChaseResult` = EQP + MAIN + vent/vac/fore* + Steps에 Takeoff → 연결정보상이에서 S.Tag≠T.Tag 검사 | `TraceAlgorithm.MainLineTakeoff.cs`, `MainLineTraceHelper.cs` |
| 3 | SP-TOF-DEFAULT | Trace 엔진 | MainLine·Water 아님 | Takeoff에서 종료. `AreRelated(takeoff.Tag, startEquipTag)`로 Match/Mismatch | STATUS=TOF | 연결정보상이: S.Tag vs T.Tag(Damper[…]) | `TraceAlgorithm.cs` |
| 4 | SP-CONT-RESOLVE | 공통 해석 | Water·MainLine continuation | (1) `Takeoff.PipeId`가 유효 Pipe면 그 PipeId를 다음 Outlet. (2) 아니면 TakeoffId를 Outlet으로 갖는 **부모 Pipe**의 다른 Outlet 중 미방문 → 다른 Takeoff / Pipe / POC | continuation 실패 시 ING | — | `TryResolveTakeoffContinuationPoint` (Water 파일) |
| 5 | SP-TAG-MATCH | Tag 비교 | 검색·검수·TOF Match | 정확일치 · 양방향 포함 · `Damper[a,b]` 대괄호 unwrap · 콤마/세미콜론 **토큰 교집합**(Scrubber `a,b`↔`a`) · 레거시 substring | 오탐 감소 | 설비정보상이·연결정보상이·TOF Match | `EquipmentTagMatcher.cs` |
| 6 | SP-GCS-EQP-SKIP | 검수 예외 | 설비정보상이 | Start 또는 Target **SubDivision1에 GCS**면 Tag 상이 검사 **제외** | GCS Stick 등 오탐 방지 | `EquipmentInfoMismatchRule` | 동명 Rule |
| 7 | SP-INOUT-SKIP | 검수 예외 | 연결정보상이 EQP | Start/Target Information이 IN/OUT이면 EQP 연결상이 **제외** | — | `ConnectionInfoMismatchRule` | 동명 |
| 8 | SP-UT-VOC | 검수 VOC | UT상이 | NOZ: IWW1↔FWW, IWW2↔NFW, CMP exhaust↔폐수, GCS SN2↔GN2, GN2↔PN2, CMP Main Drain→Nozzle PASS 등. TOF: Diff Pump GN2 Exhaust PASS; IMP+GN2+T.UT≠Exhaust면 FAIL. EQP: FORE-LINE/By Pass Connected UT 계열 검사. VENT/FORE/VAC 계열끼리 Connected UT는 동의어. `UtMapping.csv` 그룹 맵 | 예외대상이름이면 전체 스킵 | `UtilityMismatchRule` | 동명 + UtMapping.csv |
| 9 | SP-POCUT | 검수 | POCUT상이 · STATUS=NOZ | PZ&lt;5400: GO2→O2 상이. PZ≥5400: PO2→O2 상이 | 층별 O2 매핑 | `PocUtMismatchRule` | 동명 |
| 10 | SP-FIXRESOLVE | 검수 | FixResolve · EQP | Division에 Equipment 없음, 또는 동일 StartPoc 분기 1개이고 S.GUID=T.GUID | 자기회귀/비설비 | `FixResolveRule` | 동명 |
| 11 | SP-HOOKUP | Hookup | Main/Sub + Exhaust/Vac | Expected Hookup vs DynProps (파이프라인 외 고정 Rule) | MISSING/MISMATCH 등 | `HookupValidationService` | 동명 |

---

## 3. Water vs MainLine 차이

| 항목 | Water | MainLine (VOC-1) |
|------|-------|------------------|
| 판정 키 | Utility**Group**에 water | Sub1에 MAIN + Utility**Name**에 vent/vac/fore* |
| Takeoff에서 종료? | 아니오 | 아니오 |
| 형제 BFS 큐 | 유지 | **Clear (폐기)** |
| 종료 후 결과 필터 | 없음(다수 경로 가능) | EQP 있으면 **최단 1건만** |
| 재방문 Takeoff | 소멸(결과 없음) | 동일 |
| continuation 없음 | ING | ING |
| 검수 포인트 | 하류 STATUS 기준 | EQP+TOF경유 시 Tag 연결정보상이 강화 |

---

## 4. continuation Outlet 해석 순서

1. `TakeoffData.PipeId`가 캐시에 존재 → 다음 진입점 = 그 PipeId  
2. 아니면 TakeoffId가 Outlet인 부모 Pipe의 Outlet1/2/3 중  
   - 자기 Takeoff / 방문 노드 제외  
   - 다른 Takeoff → 그 ID  
   - Pipe → 그 PipeId  
   - POC → 그 InstanceId  
3. 모두 실패 → Broken(ING)

---

## 5. 운영 시 확인 팁

| 현상 | 의심 |
|------|------|
| Water인데 STATUS가 TOF로만 끝남 | UtilityGroupName에 "water" 문자열 없음(오타·다른 그룹명) |
| MAIN vent인데 TOF로 끝남 | Sub1에 MAIN 없음, 또는 UT명에 vent/vac/fore* 미포함 |
| MainLine이 자꾸 ING | continuation PipeId 미연결, 또는 EQP 미도달 |
| MainLine EQP는 맞는데 다른 분기 결과 사라짐 | 정상(형제 큐 폐기 + Prefer 최단 EQP) |
| Damper[a,b] vs 설비a 연결상이 오탐 | TagMatcher 미적용 경로인지 확인(구 로직) |
| 상세 로그 | `EnableDetailedDebug=true` → `[Water UT]` / `[MainLine]` 로그 |

---

## 6. 소스 위치

| 파일 | 역할 |
|------|------|
| `PocTrace.Core/Engine/TraceAlgorithm.cs` | Takeoff 분기 우선순위, Prefer 호출 |
| `…/TraceAlgorithm.WaterTakeoff.cs` | Water 계속 추적 + continuation 해석 |
| `…/TraceAlgorithm.MainLineTakeoff.cs` | MainLine 계속 추적 + 큐 Clear + Prefer EQP |
| `…/Validation/MainLineTraceHelper.cs` | 검수용 MainLine(TOF경유 EQP) 판별 |
| `…/Validation/EquipmentTagMatcher.cs` | Scrubber·Damper Tag |
| `…/Validation/Rules/*.cs` | VOC/예외 검수 |

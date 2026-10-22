# 철거설비 검수 — 초기 아키텍처 설계 (Rule 1 기준)

문서 버전: 2026-08-08 (결정 확정 반영)  
범위: **VOC 1)~4) + 검수 Rule 1 / Excel Sheet 1** 까지. Rule 2~7은 확장 슬롯만 정의.

---

## 0. 한 줄 결론

**새 WinExe(`PocTrace.Demolition`)를 Core만 참조**하고, BinaryCache 로드 → EQPID 배치 Trace → Rule 파이프라인 → Preview(WinForm/HTML/Excel)로 결과를 낸다.  
Rule 1은 기존 Detail Trace의 투영이며, Sheet1에 InstanceNumericId / ConstructionCode / Division + **Check Result/Issue 포함**.

exe는 **X: 공용, Viewer와 같은 경로 계열**. Core에 WinForms 금지.

---

## 0.1 Rule 2 전 확정 사항 (Locked)

| # | 질문 | 결정 |
|---|------|------|
| 1 | Sheet1 Check Result / Issue | **포함 필수** |
| 2 | Instance Id | 쿼리 `WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID)` — 인자는 **S:GUID(=Node Guid)**. DTO에 Guid와 구분된 **`long InstanceNumericId`** |
| 3 | Construction Code | `ObjectInstanceLog.CONSTRUCTIONCODE` → DTO `string ConstructionCode`. Input 엑셀 코드는 **참고만**, Rule1 필터 미사용 |
| 4 | exe 위치 | **X: 공용**, Viewer와 **같은 경로(동반 배포)**, `SharedCachePath` 동일 |
| 5 | Rule1 대상 | **EQPID 리스트 전체** Trace. 건설코드로 제외하지 않음 |

---

## 1. 목표 (Phase A)

| ID | 내용 |
|----|------|
| G1 | Input Excel Browse (default=exe 폴더) |
| G2 | Site→캐시 매핑, Site별 1회 로드 |
| G3 | Grid 미리보기 + Run |
| G4 | Rule1: EQPID 전체 Trace → Sheet1 (Check/Issue 포함) |
| G5 | Preview Grid/Search/Excel/HTML |
| G6 | Excel 7시트 골격 |

---

## 2. 배포

```
X:\jHOOK\
  PocTraceData\                 SharedCachePath (Viewer·Demolition 공통)
  Apps\PocTrace\                (현장 Viewer 실제 경로에 맞춤)
    Viewer\
    Demolition\                 PocTrace.Demolition.exe + .config
```

---

## 3. Site 매핑

| Input | Cache |
|-------|--------|
| X1, X1L | x1 |
| X2, X2-PH1 | x2 |
| X2-2, X2-PH2 | x2_2 |

---

## 4. Core 변경 계약 (A2)

### SQL `GetSelectNodes` (Equipment 등)

```sql
A.InstanceID AS InstanceId,
WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID) AS InstanceNumericId,
A.CONSTRUCTIONCODE AS ConstructionCode,
...
```

### `NodeInfo`

| 필드 | 타입 | 설명 |
|------|------|------|
| InstanceId | Guid | 기존 그래프 PK (= S:GUID) |
| InstanceNumericId | long | GetDotNetHashCode 결과 (**신규**) |
| ConstructionCode | string | ObjectInstanceLog (**신규**) |

### BinaryCache
- nodes.bin에 위 2필드 직렬화
- **v6** 권장. Admin CacheOnly/Full로 재생성 후 Demolition 사용
- v5 읽기 시 NumericId=0, ConstructionCode=null 허용(하위 호환)

### TraceResult
- `StartNodeDivision` 추가 (Sheet1 Division 열)
- `InstanceNumericId` 추가 (Sheet1 Instance Id) — MapSourceInfo에서 start NodeInfo 매핑
- `ConstructionCode` — start NodeInfo에서 매핑 (Admin 설비 쿼리로 보완 가능)

---

## 5. Sheet1 스키마 (Rule01)

선행 열:
1. Instance Id (`long`)  
2. Instance Name  
3. EQPID  
4. Construction Code  
5. Division / Subdivision1 / Subdivision2 / GUID  

이어서:
6. **Check Result, Fail Count, Issue Type, Feedback, Issue Owner**  
7. Line + Detail 잔여(S: Connector… / Status / T:* / UT* …)  
   — 기존 Detail의 S: Name/Tag/Sub/GUID는 선행열로 이동했으므로 잔여에서 제외

### Excel 시트 (VOC 현장 반영)
| 시트 | 내용 |
|------|------|
| 설비정보 | 캐시 Equipment Tag 존재만 (Trace 없음). GUID 단위. 헤더 8열 |
| Status EMP | POC Trace · Status≠EMP · Connector GUID 단위. Validation 무관 |
| Nozzle | Daily `{line}_Full_List.xlsx` · T:Info⊇nozzle · T:ConnectionTag↔EQPID |
| Stick | Full List · S:Info⊇Stick · S:ConnectionTag↔EQPID (Admin `1-%`+`6-%` 필요) |
| TakeOff | Full List · Status=TOF · T:Tag↔EQPID |
| Hook Up Number | 캐시 Pipe Hookup ⊇ EQPID |
| BIM Group | Sidecar `BimGroupCache_{line}` · ConfigurationLog.Name ⊇ EQPID → EQPID만 무양식 1열 |
| (구 nodes OUTER APPLY) | **금지** — GraphCache 타임아웃 원인 |

메타 1~3행 삭제. Sheet1~6: 헤더=1행. Sheet BIM Group: 헤더 없음. Full List는 라인당 1회 slim DTO 로드 후 Sheet 3~5 재사용.

Admin Full Excel: `S:BIM ID`, `S:Instance ID`를 `S: GUID` 왼편에 추가. `GetSelectAllEquipments`에 `OR S1.Name LIKE '6-%'`.
Admin BimGroup: `GetSelectBimGroupNames` → `BimGroupCacheRepository.Save` (GraphCache와 실패 격리).

---

## 6. 구현 단계


### Rule03 (Locked)
| 항목 | 결정 |
|------|------|
| 시작점 | **Nozzle 노드** (설비 Trace 시작점 아님) |
| 필터 | POC `Information`에 `nozzle`(대소문자 무관) **AND** `ReferenceTag`(Connection Tag)에 업로드 **EQPID** 포함 |
| 출력 열 | Instance Id, **Bim Id**, Instance Name, GUID, Connection Tag, Division, Subdivision1, Subdivision2 |
| BimId | `ObjectInstanceLog.BIMID` → SQL/DTO/`nodes.bin` (**Cache v7**) |
| 데이터 | Job 중 캐시 스캔 `Rule03Projector.Collect` |

### Rule04 (Locked)
| 항목 | 결정 |
|------|------|
| 대상 | **설비(Equipment)** · `Subdivision1`에 **GCS** 포함 |
| POC | Connector Information = **STICK** (대소문자 무시) |
| 필터 | Connection Tag에 업로드 **EQPID** 포함 |
| 헤더 | **Rule03(Sheet3)과 동일** |
| 데이터 | `Rule04Projector.Collect` |

### Rule05 (Locked)
| 항목 | 결정 |
|------|------|
| 대상 | **Takeoff** |
| 필터 | S:Tag에 업로드 **EQPID** 포함 (`Damper[eqpid]` 형태) |
| 헤더 | **Rule03/04와 동일** |
| 비고 | Bim Id / Connection Tag / Division / Sub* 는 Takeoff에 없으면 **공란** |
| 데이터 | `Rule05Projector.Collect` |

### Rule06 (Locked)
| 항목 | 결정 |
|------|------|
| 대상 | **전체 배관(Pipe)** |
| 필터 | `GetHookupNumber(PipeId)`에 업로드 **EQPID** 포함 |
| 헤더 | Rule05 양식 + **Hookup Number** 열 |
| 데이터 | `Rule06Projector.Collect` |

### Rule07 (Locked)
| 항목 | 결정 |
|------|------|
| 대상 | **전체 Node** (`EnumerateNodes`) |
| 소스 | AutoCAD `BuildBimGroupMapQuery`와 동일 — `ConfigurationItemLog`→`ConfigurationLog.Name` (`capa` 제외) |
| 필터 | `NodeInfo.BimGroupName`에 업로드 **EQPID** 포함 |
| 헤더 | Rule05 양식 + **BIM Group Name** (Revit 경로/파일명) |
| 캐시 | **v8** `nodes.bin` += BimGroupName |
| 데이터 | `Rule07Projector.Collect` |

### Rule02 (Locked)
| 항목 | 결정 |
|------|------|
| 입력 | Rule01과 동일 Trace 결과 집합 |
| 필터 | `FailCount >= 1` **AND** `STATUS != EMP` |
| 행 전개 | `TraceResult.Steps`의 **캐시 GUID 객체 전부** (Node/Pipe/Takeoff/Poc). Hookup은 참조만 — **제외 없음** |
| 헤더 | Rule01에서 **Check Result / Fail Count / Issue Type 3열 제외** |
| Instance Id | Node = 캐시 `InstanceNumericId`(0이면 Guid 해시). **Pipe = PipeId의 GetDotNetHashCode 계열 — 공란 금지** |
| EQPID | **전 행·전 Rule** = 업로드 Excel EQPID(=시작 설비 Tag) |
| 빈 칸 | 채울 수 없는 열은 공란 |
| 구현 | Job 중 캐시 생존 구간에 `Rule02Projector.Expand` → `DemolitionJobResult.Rule02Rows` |

---

## 6. 구현 단계

| Step | 작업 |
|------|------|
| **A0** ✅ | `PocTrace.Demolition` WinExe 스캐폴딩 |
| **A1** ✅ | Input Excel Reader + Browse(기본=exe폴더) + Grid 바인딩/검증색 + Site 매핑 |
| **A2** ✅ | SQL + NodeInfo + Cache v6 + StartNodeDivision / InstanceNumericId 매핑 |
| **A3** ✅ | Job: Site 로드 + EQPID 전체 Trace (`DemolitionInspectionJob`) |
| **A4** ✅ | Rule01 투영 + 7시트 Excel (`DemolitionExcelExporter`) |
| **R02/R03** ✅ | Rule02 경로 전개 + Rule03 Nozzle/BimId(Cache v7) |
| **R04** ✅ | Rule04 GCS STICK ConnectionTag↔EQPID (Sheet3 헤더 동일) |
| **R05** ✅ | Rule05 Takeoff Tag Damper[EQPID] |
| **R06** ✅ | Rule06 Pipe Hookup Number ↔ EQPID |
| **R07** ✅ | Rule07 BIM Group Name ↔ EQPID (Cache v8) |
| A5 | Preview + HTML |
| A6 | X: Viewer 동반 배포 |

---

## 7. 다음 액션

Rule01~07 Excel 완료. 다음: A5 Preview/HTML 또는 Admin Cache v8 재생성 후 VDI 검증.

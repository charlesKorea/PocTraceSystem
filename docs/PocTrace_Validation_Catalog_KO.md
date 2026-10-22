# PocTrace 검수 항목 카탈로그 (KO)

> Excel로 쓰려면: 같은 폴더의 `PocTrace_Validation_Catalog_KO.csv`를 Excel에서 열기  
> 또는 아래 표를 전체 선택 → 복사 → Excel에 붙여넣기

| No | RuleId | 이슈 제목(리포트) | 분류 | 대상 STATUS | 검수 내용(상세) | 예외/비고 | 귀속 힌트 | 구현 |
|----|--------|-------------------|------|-------------|-----------------|-----------|-----------|------|
| 1 | 철거설비연결 | 철거설비연결 | 연결/상태 | ERR | Trace 종료가 ERR(철거·Delete 등)이면 이슈. Feedback: 건설코드 Delete 포함 | 없음 | SCS | ErrStatusRule |
| 2 | 모델링미완료 | 모델링 미완료 | 모델링 | ING | ING이며 시작 POC UtilityGroup·ConnectorName이 예외대상이 아니면 미완료. Feedback에 T.Name | 예외대상이름 스킵 | 설계사 | ModelingIncompleteRule |
| 3 | 설비정보상이 | 기준정보 이슈 | 기준정보 | EQP | S.Tag(StartEquipmentTag)≠T.Tag. Scrubber/콤마/Damper[a,b]는 TagMatcher로 관련 허용 | Start/Target Sub1에 GCS면 제외 | SCS | EquipmentInfoMismatchRule |
| 4 | POCUT상이 | 기준정보 이슈 | 기준정보 | NOZ | PZ&lt;5400: GO2→O2 상이. PZ≥5400: PO2→O2 상이 | 해당 조합만 | SCS | PocUtMismatchRule |
| 5 | 모델링없음 | 모델링 없음 | 모델링 | EMP | EMP이고 S.UT에 '미사용' 없으면 모델링 없음 | UT에 미사용 있으면 스킵 | 설계사 | ModelingAbsentRule |
| 6 | 연결정보누락 | 연결정보 누락 | 연결정보 | TOF/NOZ/EQP | TOF: T.Tag 공백. NOZ: T.ConnectionTag 공백. EQP+GCS+STICK: Connector Tag 공백 | GCS STICK만 EQP 누락 | 설계사 | ConnectionMissingRule |
| 7 | 연결정보상이 | 연결정보 상이 | 연결정보 | TOF/NOZ/EQP | MainLine VOC, TOF/NOZ Tag, Scrubber, GCS STICK, MAIN/SUB/PCS/GCS, 분기 불일치 | IN/OUT 제외. T가 S 포함 시 정상 | 설계사 | ConnectionInfoMismatchRule |
| 8 | FixResolve필요 | Fix Resolve 필요 | 구조 | EQP | Division≠Equipment 또는 단부분기 S.GUID=T.GUID | 분기 1개일 때만 GUID 조건 | 설계사 | FixResolveRule |
| 9 | UT상이 | UT 상이 / (공백) | Utility | NOZ/TOF/EQP | VOC 예외 후 UT 공백 또는 Connected UT 불일치(UtMapping·VENT/FORE/VAC 계열) | 예외 UT/Name/Group 스킵 | 설계사 | UtilityMismatchRule |
| 10 | HookupValidation | MISSING/MISMATCH 등 | Hookup | Exhaust·Vac | Main/Sub + Exhaust/Vac만. Expected Hookup vs DynProps | 고정 1 Rule | SCS/운영 | HookupValidationService |
| 11 | (선처리) | 기준정보이슈 | 기준정보 | EQP/NOZ/TOF | 예외대상 Utility면 POC Utility 상이 | 파이프라인 외 | SCS | TraceValidator |
| 12 | (선처리) | 모델링 미완료 | 모델링 | ING | 예외 UT 또는 elec 그룹 | 파이프라인 외 | 설계사 | TraceValidator |

## STATUS 약어
| STATUS | 의미 |
|--------|------|
| EQP | 다른 설비 POC로 도착 |
| NOZ | Nozzle 종단 |
| TOF | Takeoff(Damper) 종단 |
| CAP | Cap/마감 |
| EMP | 연결 없음/Empty |
| ING | 미완성·루프·끊김 등 Broken |
| ERR | 데이터/철거 등 오류 |

## 귀속 규칙(요약)
- 이슈 제목에 **기준정보** 또는 **장비** 포함 → 귀속 **SCS**, HU 카운트 제외
- 그 외 설계 귀책 이슈 → **설계사(设计社)**, invalidCountForHU 증가

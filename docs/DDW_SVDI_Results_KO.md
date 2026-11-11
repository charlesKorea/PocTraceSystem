# DDW SVDI 조회 결과 기록

이 문서는 SVDI(사내망)에서 `docs/ddw-svdi-queries`를 돌린 결과와 거기서 나온 결정을 쌓는 곳이다. 전략과 쿼리 본문은 `DDW_Cache_Strategy_KO.md`가 맡고, 이 문서는 사실, 결정, 후속 작업만 유지한다.

## 사용법

1. 쿼리 파일은 한 번에 하나씩 SVDI에서 돌린다. 회신은 `Q=번호`, `분=`, `결과=` 형식이다. 에러는 메시지 앞 300자만 보낸다.
2. 결과를 받으면 아래 3절에 그 쿼리 항목을 채우고, 4절(결정)과 5절(코드 영향)을 갱신한다. 값은 요약해서 적는다. GUID 전문, Tag 전문, 계정, 서버 주소는 넣지 않는다.
3. 쿼리를 고쳤으면 `ddw-svdi-queries/`, `DDW-SVDI-Queries.zip`, 전략 문서 13절을 같이 고친다. 고친 날짜는 쿼리 머리 주석의 `rev` 표시로 남긴다.
4. 아래 2절 진행 표의 상태를 바꾼다.

## 1. 확정한 사실

| 항목 | 값 | 근거 |
|---|---|---|
| SSMS 접속 | FRM 서버. Linked Server `remote_monami` | 01-A. 다른 링크 서버도 있으나 이 작업과 무관하다 |
| 원격 카탈로그 | `monami` | 01-B |
| 4-part 이름 | `remote_monami.monami.dbo.tb_*` | 01-C로 5개 테이블 읽힘 |
| 삭제 판정 열 | `operationtype` 열 없음. `version_sequence`가 S5D의 `OperationType`에 해당하며 임계 이상이면 삭제 | 01-C에서 `tb_instancegroups`의 `operationtype` 선택이 오류. 담당자가 이 DDW 전체에 열이 없다고 확인 |
| `version_sequence` 확인 테이블 | `tb_modelinstances` (샘플 값 2682) | 01-C |

임계 값은 아직 하나로 고정하지 않았다. 담당자 설명은 "10000000 이상이면 삭제"이고, JSearchMK 주석은 `1000000000`이다. 자릿수가 다르다. SLOW-01의 분포가 오기 전에는 어느 쪽도 캐시 SQL에 쓰지 않는다.

## 2. 진행

| 순서 | 쿼리 | 상태 | 비고 |
|---|---|---|---|
| 1 | 01-probe | 완료 2026-11-11 | 3절 |
| 2 | 01B-live-columns | 대기 | tb_* 별 `operationtype`, `version_sequence` 열 유무. 먼저 돌린다 |
| 3 | 02-tables-and-rows | 대기 | |
| 4 | 03-columns-core | 대기 | |
| 5 | 04-columns-other | 대기 | |
| 6 | 05-name-hits | 대기 | D-01 (TbStar/WebDdw) |
| 7 | 06-objects-like-version-hook | 대기 | |
| 8 | 07-division-vocab | 대기 | rev2: live 필터 없음 |
| 9 | 08-small-masters | 대기 | rev2 |
| 10 | 09-group-shape | 대기 | rev2 |
| 11 | 10-shape-samples | 대기 | rev2. model 만 `version_sequence` 선택 |
| 12 | 11-template-sub-top | 대기 | rev2 |
| 13 | SLOW-01 | 대기 | `version_sequence` 분포. 임계 1e9 vs 1e7 |
| 14 | SLOW-02 ~ SLOW-06, SLOW-08 | 대기 | rev2. 삭제 행을 포함한 전체 행 기준 |
| 15 | SLOW-07 | 대기 | 가장 마지막 |

## 3. 결과

### 01-probe (2026-11-11, rev1 쿼리로 실행)

| 블록 | 결과 |
|---|---|
| 01-A | 링크 서버는 `remote_monami` 외에도 있다. 무시한다 |
| 01-B | 카탈로그 `monami` |
| 01-C `tb_instancegroups` | `has_id` 있음. `operationtype` 선택은 오류(열 없음) |
| 01-C `tb_modelinstances` | `model_type` 50462981, `composite_yn` 0, `version_sequence` 2682, `posz` 15050 |
| 01-C `tb_pocinstances` | `owner_type` 768, `connection_order` 0, `connected` set |
| 01-C `tb_pipeinstances` | `pipe_type` 17301760, `length1` 1154.445 |
| 01-C `tb_attributes` | `attribute_nm` ConvertDate, `owner_type` 1 |

읽는 법:

- `pipe_type` 17301760은 Takeoff(17301768)가 아닌 일반 배관이다.
- `length1` 1154.445와 `posz` 15050은 크기로 보아 mm로 보이나 추정이다. SLOW-05의 최소·평균·최대로 확정한다.
- 이 실행의 `tb_pocinstances`와 `tb_pipeinstances`에는 `operationtype`을 뺀 형태로 선택했다. 그 두 테이블의 `version_sequence` 유무는 01B로 확인한다.

### 01B 이후

회신이 오면 이 아래에 `### 쿼리 이름 (날짜)` 절을 추가한다.

## 4. 결정 기록

| ID | 내용 | 상태 | 비고 |
|---|---|---|---|
| D-01 | 실행 DB가 TbStar인지 WebDdw인지 | 대기 | 05 결과 |
| D-10 | 삭제 판정을 `version_sequence`로 한다. `operationtype` 열은 이 DDW 쿼리와 캐시 SQL에서 제외한다 | 부분 확정 | 열 없음과 대체 열은 확정. 임계와 적용 테이블 범위는 01B, SLOW-01 후 |
| 쿼리 팩 | `operationtype`을 쓰던 쿼리를 전부 고쳤다(rev2). `01B`를 추가했다 | 완료 2026-11-11 | 삭제 행이 세어지는 쿼리가 있다 |

## 5. 코드 영향 (아직 고치지 않음)

전략 문서 2절의 방침대로 이 단계에서는 C#을 바꾸지 않는다. 아래는 `monami`에서 DDW 캐시를 만들기 전에 닫아야 할 목록이다.

| 위치 | 현재 | 문제 |
|---|---|---|
| `PocTrace.Core/Infrastructure/Data/DdwSql.cs` | 모델, POC, 배관, 속성, 마스터 조인 전부 `OPERATIONTYPE < 2`이고 `AS OperationType`을 선택한다 | 이 사이트에서는 열이 없어 쿼리 오류 |
| `PocTrace.Core/Infrastructure/Data/DdwGraphMapping.cs` | `OperationType >= 2`인 행을 버린다 | live 판정 열이 바뀌면 값의 의미를 다시 정해야 한다 |
| `PocTrace.DdwAcceptance/Program.cs` | `OPERATIONTYPE < 2`가 SQL에 있는지 검사한다 | 기대가 바뀐다 |

방향: 01B로 테이블별 열 유무를 확인하고, SLOW-01로 임계를 정한 뒤, 프로파일이 live 문을 `version_sequence < 임계`로 만든다. 마스터 테이블(템플릿, 유틸리티 등)에 `version_sequence`가 없으면 그 테이블은 live 조건 없이 조인한다.

## 6. 열린 질문

- `operationtype`이 이 DDW의 어느 테이블에도 없는지(01B). 담당자 설명은 전체 없음이다.
- 임계 값이 1e9인지 1e7인지(SLOW-01).
- `tb_pocinstances`, `tb_pipeinstances`, `tb_attributes`, `tb_instancegroups`에 `version_sequence`가 있는지(01B).
- 직접 `monami` 카탈로그에 붙는 접속이 있는지, `remote_monami`뿐인지. 캐시 작성은 직접 접속을 쓴다.

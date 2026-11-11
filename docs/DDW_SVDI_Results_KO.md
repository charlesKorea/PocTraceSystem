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
| 삭제 판정 열 | `tb_` 로 시작하는 모든 테이블은 DDW DB이고 `operationtype` 열이 없으며 `version_sequence`가 있다. `version_sequence`가 S5D의 `OperationType`에 해당하며 임계 이상이면 삭제 | 01-C에서 `tb_instancegroups`의 `operationtype` 선택이 오류. 담당자 확인(2026-11-11) |
| `version_sequence` 값 확인 | `tb_modelinstances` 샘플 2682 | 01-C. 다른 테이블 값은 SLOW-01로 본다 |
| 규모 | 모델 1,022,126, POC 1,469,668, 배관 228,878, 속성 11,953,709 | 02. 3절 |

임계 값은 아직 하나로 고정하지 않았다. 담당자 설명은 "10000000 이상이면 삭제"이고, JSearchMK 주석은 `1000000000`이다. 자릿수가 다르다. SLOW-01의 분포가 오기 전에는 어느 쪽도 캐시 SQL에 쓰지 않는다.

## 2. 진행

| 순서 | 쿼리 | 상태 | 비고 |
|---|---|---|---|
| 1 | 01-probe | 완료 2026-11-11 | 3절 |
| 2 | 01B-live-columns | 선택 | 담당자 확인으로 대체. 결과로 남기고 싶을 때만 |
| 3 | 02-tables-and-rows | 부분 접수 2026-11-11 | 중요 테이블 16개. 3절 |
| 4 | 03-columns-core | 대기 | |
| 5 | 04-columns-other | 대기 | |
| 6 | 05-name-hits | 대기 | D-01 (TbStar/WebDdw) |
| 7 | 06-objects-like-version-hook | 대기 | |
| 8 | 07-division-vocab | 대기 | rev2: live 필터 없음 |
| 9 | 08-small-masters | 보류 | rev2. `tb_pipesize`, `tb_poctypes`, `tb_new_usergroups` 이름 확인 후 |
| 10 | 09-group-shape | 대기 | rev2 |
| 11 | 10-shape-samples | 대기 | rev2. model 만 `version_sequence` 선택 |
| 12 | 11-template-sub-top | 대기 | rev2 |
| 13 | SLOW-01 | 대기 | 모델, 배관, POC의 `version_sequence` 분포. 임계 1e9 vs 1e7. 행수가 작아 10분 안에 끝날 것으로 본다 |
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
- 이 실행의 `tb_pocinstances`와 `tb_pipeinstances`에는 `operationtype`을 뺀 형태로 선택했다. `version_sequence`는 담당자 확인으로 모든 `tb_*`에 있어서 10 샘플과 SLOW-01이 두 테이블에서도 그 열을 읽는다.

### 02-tables-and-rows (2026-11-11, 중요 테이블 16개만 접수)

`sys.partitions` 행수라 정확한 COUNT가 아니고, live 필터가 없어서 삭제 행을 포함한다.

| 테이블 | 행수 | 캐시에서 쓰임 |
|---|---:|---|
| `tb_attributes` | 11,953,709 | 속성 두 이름(`Element ID_Number`, `__MODULE_INFO`) |
| `tb_categories` | 631 | 현재 캐시 SQL에 없음 |
| `tb_division` | 770 | 모델 템플릿의 Division, Sub1, Sub2 이름 |
| `tb_instanceaabbs` | 2,731,234 | 현재 캐시 SQL에 없음 |
| `tb_instancegroupmembers` | 1,260,579 | BimGroup 사이드카 후보 |
| `tb_instancegroups` | 8,057 | BimGroup 사이드카 후보 |
| `tb_modelinstances` | 1,022,126 | 노드 |
| `tb_modeltemplates` | 148,110 | 노드 분류 |
| `tb_new_users` | 195 | 배관 작업자·회사 |
| `tb_pipeinstances` | 228,878 | 배관과 Takeoff |
| `tb_pocinstances` | 1,469,668 | 모델 POC(768)와 배관 끝점(256) |
| `tb_poctemplates` | 193,150 | POC 유형, Takeoff 플래그 |
| `tb_utilities` | 44,482 | Utility |
| `tb_utility_groups` | 48 | Utility 그룹 |
| `tb_utility_compatible` | 254 | 현재 캐시 SQL에 없음 |
| `tb_versions` | 4,591 | 워터마크 후보(미구현). live 판정에 조인하지 않는다 |

읽는 법:

- 쿼리 팩 주석과 전략 문서 초안의 규모 가정이 이 DB와 다르다. 속성은 1.8억이 아니라 약 1,195만, 템플릿은 약 120만이 아니라 148,110, 그룹은 약 9만이 아니라 8,057이다. 주석은 02 결과로 고쳤다.
- 큰 테이블이 모델 102만, POC 147만 정도라 SLOW 쿼리들은 초안이 가정한 것보다 빨리 끝날 것으로 본다. 속성이 가장 크다(1,195만).
- 이 수치로 본 추정: POC 147만은 모델 POC와 배관 끝점(배관당 대략 둘)이 섞인 수다. 정확한 구성은 SLOW-03이 준다.
- 현재 캐시 SQL이 읽지 않는 테이블이 있다: `tb_categories`, `tb_instanceaabbs`, `tb_utility_compatible`. 캐시 범위에 넣을지는 03, 04 열 명세를 본 뒤 정한다.

회신에 없는 테이블: `tb_pipesize`, `tb_poctypes`, `tb_new_usergroups`. 쿼리 팩 08이 이 셋을 읽는다. 중요 테이블만 골라 보낸 것이면 문제 없고, DB에 없다면 08이 오류가 난다. 03, 04로 이름이 확인될 때까지 08은 보류한다.

### 03 이후

회신이 오면 이 아래에 `### 쿼리 이름 (날짜)` 절을 추가한다.

## 4. 결정 기록

| ID | 내용 | 상태 | 비고 |
|---|---|---|---|
| D-01 | 실행 DB가 TbStar인지 WebDdw인지 | 대기 | 05 결과 |
| D-10 | 삭제 판정을 `version_sequence`로 한다. `operationtype` 열은 이 DDW 쿼리와 캐시 SQL에서 제외한다 | 부분 확정 | 모든 `tb_*`에 `version_sequence` 있음, `operationtype` 없음(담당자 확인). 임계(1e9 vs 1e7)는 SLOW-01 후 |
| 쿼리 팩 | `operationtype`을 쓰던 쿼리를 전부 고쳤다(rev2). `01B`를 추가했다 | 완료 2026-11-11 | 삭제 행이 세어지는 쿼리가 있다 |

## 5. 코드 영향 (아직 고치지 않음)

전략 문서 2절의 방침대로 이 단계에서는 C#을 바꾸지 않는다. 아래는 `monami`에서 DDW 캐시를 만들기 전에 닫아야 할 목록이다.

| 위치 | 현재 | 문제 |
|---|---|---|
| `PocTrace.Core/Infrastructure/Data/DdwSql.cs` | 모델, POC, 배관, 속성, 마스터 조인 전부 `OPERATIONTYPE < 2`이고 `AS OperationType`을 선택한다 | 이 사이트에서는 열이 없어 쿼리 오류 |
| `PocTrace.Core/Infrastructure/Data/DdwGraphMapping.cs` | `OperationType >= 2`인 행을 버린다 | live 판정 열이 바뀌면 값의 의미를 다시 정해야 한다 |
| `PocTrace.DdwAcceptance/Program.cs` | `OPERATIONTYPE < 2`가 SQL에 있는지 검사한다 | 기대가 바뀐다 |

방향: SLOW-01로 임계를 정한 뒤, 프로파일이 live 문을 `version_sequence < 임계`로 만든다. 모든 `tb_*`에 열이 있으므로 마스터 테이블(템플릿, 유틸리티 등) 조인에도 같은 조건을 건다.

## 6. 열린 질문

- 임계 값이 1e9인지 1e7인지(SLOW-01).
- 마스터 테이블(템플릿, 유틸리티 등)의 `version_sequence`도 같은 임계로 삭제를 판정하는지. 값 분포는 SLOW-01이 모델, 배관, POC만 본다.
- `tb_pipesize`, `tb_poctypes`, `tb_new_usergroups`가 이 DB에 있는지(03, 04).
- 직접 `monami` 카탈로그에 붙는 접속이 있는지, `remote_monami`뿐인지. 캐시 작성은 직접 접속을 쓴다.

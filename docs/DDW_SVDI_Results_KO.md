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
| 삭제 판정 열 | `tb_` 로 시작하는 모든 테이블은 DDW DB이고 `operationtype` 열이 없으며 `version_sequence`가 있다. `version_sequence`가 S5D의 `OperationType`에 해당한다 | 01-C에서 `tb_instancegroups`의 `operationtype` 선택이 오류. 담당자 확인(2026-11-11) |
| 삭제 임계 | **모든 테이블 쿼리에 `version_sequence < 1000000000`이 기본.** 그 이상은 삭제 행이라 제외 | 담당자 확정(2026-11-12). 11일의 "10000000 이상" 설명은 이 값으로 정리한다. JSearchMK 주석과도 같다 |
| Tag 원천 | 설비 Tag는 `tb_attributes`의 `attribute_nm = 'tag'`, `attribute_val = <TAG>`이고 그 행의 `owner_id`가 설비 인스턴스 id | 담당자 설명(2026-11-12). 3절 |
| 설비 판정(담당자 기준) | `tb_modelinstances.model_type = 50594049`, 템플릿 `family_key = equipment`, `bim_yn = 0`이면 2차 설계 설비 | 담당자 설명. 코드 가정과 충돌하는 부분은 6절 Q-03 |
| 배관 길이 | `tb_pipeinstances.length2`가 길이(mm, 변환 없음) | 담당자 설명. 현재 코드는 `length1`. 6절 Q-01 |
| 표기 | 대화에서 `*`만 적으면 `remote_monami.monami.dbo.tb_*`의 그 테이블 | 담당자 안내(2026-11-12) |
| `version_sequence` 값 확인 | `tb_modelinstances` 샘플 2682 | 01-C. 다른 테이블 값은 SLOW-01로 본다 |
| 규모 | 모델 1,022,126, POC 1,469,668, 배관 228,878, 속성 11,953,709 | 02. 3절 |

임계는 `1000000000`(1e9)으로 정리했다. SLOW-01의 분포는 이 값이 실제 값 사이의 틈에 있는지 확인하는 용도로 남긴다.

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
| 13 | SLOW-01 | 대기 | 모델, 배관, POC의 `version_sequence` 분포. 임계 1e9 확정 후라 검증용. 행수가 작아 10분 안에 끝날 것으로 본다 |
| 14 | SLOW-02 ~ SLOW-06, SLOW-08 | 대기 | rev2. 삭제 행을 포함한 전체 행 기준 |
| 15 | SLOW-07 | 대기 | 가장 마지막 |
| 16 | T01-flow-steps | 대기 | 대표 설비 1건. 담당자 flow를 쿼리로 옮긴 것. 열 이름 확인 때문에 03, 04 뒤에 돌린다 |
| 17 | T02-tag-attribute-shape | 대기 | Q-04 |
| 18 | T03-equipment-judgement | 대기 | Q-03, Q-07 |
| 19 | T04-poc-shape | 대기 | Q-06, Q-07, Q-09. D 블록이 가장 길다 |
| 20 | T05-pipe-length-deleted | 대기 | Q-01, Q-11. `last_update` 열이 없으면 C, D 블록만 에러 |
| 21 | T06-history-rows | 대기 | Q-10 |

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

### 담당자 설명: 설비 Tag에서 배관까지 (2026-11-12, 쿼리 결과가 아니라 SVDI 화면과 테이블 추적)

실제 설비 Tag 1건을 DDW 솔루션과 `monami` 테이블로 따라간 결과다. Tag, 모델 이름, GUID 전문은 적지 않는다. 전체 단계는 `DDW_Trace_Flow_KO.md` 3절이다.

| 단계 | 테이블 | 확인된 사실 |
|---|---|---|
| 1 | `attributes` | `attribute_nm = 'tag'`와 Tag 값으로 `owner_id`(설비 인스턴스)를 얻는다 |
| 2 | `modelinstances` | `instance_id = owner_id`. `model_type = 50594049`이면 설비. `model_template_id`는 라이브러리 모델 id |
| 3 | `modeltemplates` | `display_nm`이 DDW 객체 이름과 같다. `family_key = equipment`, `bim_yn = 0`(2차 설계) |
| 4 | `poctemplates` | `poc_template_nm`, `poc_template_id`, `utility_id`. `equipment_yn = 1`이면 설비 POC. `memo`는 비고 |
| 5 | `pocinstances` | `poc_template_id`와 설비 id를 같이 걸어야 그 설비의 POC가 나온다. `connected_poc_id`가 S5D의 outlet |
| 6 | `pocinstances` | `instance_id = connected_poc_id`. 상대 POC의 `connected_poc_id`는 건너온 POC를 되가리킨다. 지나온 POC로는 가지 않는다 |
| 7 | `modelinstances`, `pocinstances` | 상대 소유 객체가 `model_type = 51380485`(connector_pipe)이면 그 객체의 POC가 둘 이상 나온다. 안 지난 POC로 잇는다 |
| 8 | `pipeinstances` | `poc_template_id`가 0인 POC의 `owner_instance_id`가 배관. `length2`가 길이(mm), 구경 값은 19 |

이 설명에서 이어지는 질문은 6절이다.

### 03 이후

회신이 오면 이 아래에 `### 쿼리 이름 (날짜)` 절을 추가한다.

## 4. 결정 기록

| ID | 내용 | 상태 | 비고 |
|---|---|---|---|
| D-01 | 실행 DB가 TbStar인지 WebDdw인지 | 대기 | 05 결과 |
| D-10 | 삭제 판정을 `version_sequence < 1000000000`으로 한다. `operationtype` 열은 이 DDW 쿼리와 캐시 SQL에서 제외한다 | 확정 2026-11-12 | 모든 `tb_*`에 `version_sequence` 있음, `operationtype` 없음. 임계 1e9는 담당자 확정. 마스터 테이블에도 같은 조건을 건다고 이해했으며 Q-12로 확인 |
| D-11 | 설비 Tag 원천은 `attributes`의 `tag` 행이다 | 담당자 설명 2026-11-12 | 전략 문서 5절 "TbStar Tag NULL"이 이것으로 메워진다. 실제 열 이름과 속성 변형은 T02 후 |
| D-12 | 배관 길이 열이 `length2`인가 | 대기 | T05 후 |
| 쿼리 팩 | `operationtype`을 쓰던 쿼리를 전부 고쳤다(rev2). `01B`를 추가했다 | 완료 2026-11-11 | 삭제 행이 세어지는 쿼리가 있다 |
| 쿼리 팩 rev4 | T01~T06 추가(Tag flow, Tag 속성, 설비 판정, POC 모양, 배관 길이와 삭제 행, 이력 행) | 완료 2026-11-12 | 모든 T 쿼리는 `version_sequence < 1000000000`. T05-C, T05-D, T06은 삭제 행·전체 행 기준 |

## 5. 코드 영향 (아직 고치지 않음)

전략 문서 2절의 방침대로 이 단계에서는 C#을 바꾸지 않는다. 아래는 `monami`에서 DDW 캐시를 만들기 전에 닫아야 할 목록이다.

| 위치 | 현재 | 문제 |
|---|---|---|
| `PocTrace.Core/Infrastructure/Data/DdwSql.cs` | 모델, POC, 배관, 속성, 마스터 조인 전부 `OPERATIONTYPE < 2`이고 `AS OperationType`을 선택한다 | 이 사이트에서는 열이 없어 쿼리 오류 |
| `DdwSql.cs` 모델 SQL | TbStar에서 Tag가 `NULL`. 속성은 `Element ID_Number`, `__MODULE_INFO`만 | 설비 Tag는 `attributes`의 `tag` 행이다(D-11). 노드 Tag를 이것으로 채워야 `FindStartPocs`가 설비를 찾는다 |
| `DdwSql.cs` 배관 SQL | `LENGTH1`을 길이로 읽는다 | 담당자는 `length2`가 길이라고 한다(D-12) |
| `DdwGraphMapping.cs` Classify | `MODEL_TYPE` 50594049/50594048만으로 설비를 만들지 않는다. connector_pipe(51380485) 처리가 없다 | Q-03, Q-07 |
| `DdwGraphMapping.cs` 끝점 | `OWNER_TYPE = 256`을 배관 끝점으로 본다 | 담당자는 `poc_template_id = 0`으로 배관을 알아본다. 같은 집합인지 Q-06 |
| `PocTrace.Core/Infrastructure/Data/DdwGraphMapping.cs` | `OperationType >= 2`인 행을 버린다 | live 판정 열이 바뀌면 값의 의미를 다시 정해야 한다 |
| `PocTrace.DdwAcceptance/Program.cs` | `OPERATIONTYPE < 2`가 SQL에 있는지 검사한다 | 기대가 바뀐다 |

방향: 프로파일이 live 문을 `version_sequence < 1000000000`으로 만든다. 모든 `tb_*`에 열이 있으므로 마스터 테이블(템플릿, 유틸리티 등) 조인에도 같은 조건을 건다.

## 6. 열린 질문

설비 Tag flow에서 나온 질문(Q-01~Q-12). 설명과 쿼리 매핑은 `DDW_Trace_Flow_KO.md` 5절이다.

| 번호 | 질문 | 쿼리 | 상태 |
|---|---|---|---|
| Q-01 | 배관 길이가 `length2`인가 `length1`인가 | T05-A, T05-B | 대기 |
| Q-02 | 구경 열 이름, POC 구경과 배관 구경의 연결, `pipesize` 테이블 유무 | 03, 04, 08 | 대기 |
| Q-03 | 설비 판정: `model_type` 50594049/50594048, `family_key = equipment`, `bim_yn` | T03 | 대기 |
| Q-04 | Tag 속성 이름 변형, `owner_type`, 중복, 1차 BIM 설비의 Tag 속성 | T02 | 대기 |
| Q-05 | 시작 POC가 설비의 POC 전부인가 `equipment_yn = 1`만인가 | T01 4, 5단계 | 대기 |
| Q-06 | `poc_template_id = 0`과 `owner_type = 256`이 같은 집합인가 | T04-A, T04-C | 대기 |
| Q-07 | connector_pipe의 POC 수와 길이 취급, 추적 중 만나는 `model_type`들 | T04-B, T03-B, SLOW-06 | 대기 |
| Q-08 | 종료 조건(다른 설비, 노즐, 캡, Takeoff)을 DDW에서 무엇으로 식별하나 | SLOW-08, SLOW-02, 대표 1건 | 대기 |
| Q-09 | 연결의 대칭성과 dangling | T04-D, SLOW-04 | 대기 |
| Q-10 | `instance_id`당 행이 여러 개인가, 삭제가 새 행인가 제자리 갱신인가 | T06 | 대기 |
| Q-11 | 삭제 행이 남아 있는지, 삭제 시각을 알 수 있는지 | T05-C, T05-D, SLOW-01 | 대기 |
| Q-12 | `memo`를 비고로 쓰는지, 마스터 테이블에도 같은 임계인지 | 담당자 답변 | 대기 |

그 밖에:

- `tb_pipesize`, `tb_poctypes`, `tb_new_usergroups`가 이 DB에 있는지(03, 04).
- 직접 `monami` 카탈로그에 붙는 접속이 있는지, `remote_monami`뿐인지. 캐시 작성은 직접 접속을 쓴다.
- 담당자가 쓴 `pocinstances.owner_id`는 코드의 `owner_instance_id`와 같은 열이라고 이해했다. 03 결과에서 열 이름을 확인한다.

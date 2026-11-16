# DDW POC 추적 최초 설계

작성일: 2026-11-16. 시험 도구 `PocTrace.DdwTraceBench`로 한 설비를 따라본 결과와, 그 분석에 대한 답을 모은 설계다. 캐시 절차의 이전 기준은 `DDW_Cache_Strategy_KO.md`, Tag에서 배관까지의 조회 흐름은 `DDW_Trace_Flow_KO.md`다. 이 문서와 어긋나면 추적 규칙과 캐시 라인은 이 문서를 따른다.

이 문서는 설계다. 운영 `DdwSql`의 `OPERATIONTYPE`은 아직 코드에 남아 있다. 그 교체가 첫 구현이다.

## 1. 구조

추적 엔진은 하나다. S5D와 DDW의 차이는 SQL 로더와 판정 객체 두 곳에만 둔다.

```
S5D 로그  ── S5dLoader ──┐
                          ├─ 같은 캐시 레코드(키는 Guid) ─ TraceSession
DDW tb_*  ── DdwLoader ──┘         │
                                   └─ 규칙: S5dRules 또는 DdwRules
```

캐시 레코드는 지금 UI가 읽는 노드, POC, 배관, Takeoff, 동적속성과 같다. 엔진은 테이블 이름을 모른다. 한 걸음은 항상 이렇다.

1. 현재 POC의 `connected` 쪽으로 간다. S5D에서는 outlet이 그 연결이고, DDW에서는 `connected_poc_id`다.
2. 도착 POC의 owner가 다음 객체다.
3. 그 owner의 POC 중 아직 가지 않은 id로만 이어 간다.

S5D의 Outlet1~3은 로더가 POC와 연결로 풀어 같은 걸음에 올린다. 엔진에 outlet 칸을 새로 만들지 않는다. DDW에 outlet 열이 없어도 같은 엔진이 걷는다.

규칙 객체만 스키마별로 둔다. Water, MainLine처럼 나중에 켤 분기는 `DdwRules` 안의 빈 자리로 남겨 두고, 엔진을 복제하지 않는다.

id는 쿼리 경계에서 `uniqueidentifier`로 맞춘다. 캐시와 엔진은 `Guid`만 본다. `0x` 문자열은 시험 도구 Excel에만 남긴다. 템플릿의 division 세 열은 GUID 문자열이므로 `TRY_CAST(열 AS uniqueidentifier)`로 `tb_division`에 맞추고, 노드에는 `division_value` 세 글자를 붙인다. 글자가 빈 노드는 Sub 어휘 판정이 같이 빈다.

## 2. 확정

| 번호 | 결정 |
|---|---|
| 1 | DDW 조회에서 `OPERATIONTYPE`을 모두 뺀다. 그 열은 DDW에 없다. live는 `version_sequence < 1000000000`이다. 모델, POC, 배관, 속성, 템플릿, division, 유틸리티 조인에 같은 조건을 건다. 한 SQL에 두 열을 섞지 않는다 |
| 2 | 특수 판정은 스키마를 가른다. S5D Cap은 지금 이름 목록이다. DDW Cap은 `bim_yn = 0`이다. DDW의 그 외 객체 분류는 이름과 division 세 값을 본다 |
| 3 | Water Takeoff, MainLine Takeoff는 DDW에서 보류다. 자리가 필요해지면 `DdwRules`에 추가한다. 그 전까지 DDW Takeoff는 멈추는 기본 경로만 탄다 |
| 4 | Takeoff Tag와 시작 설비 Tag 비교는 S5D와 같다. 3번의 특수 경로는 빼도 기본 비교는 같다. 시작 설비로 돌아오면 S5D와 같이 처리한다. 배관이 아닌 객체에서 더 갈 수 없으면 `ing`다 |
| 5 | 건설코드는 `tb_attributes.attribute_nm = 'Construction-code'`다. 설비 구분은 S5D처럼 division, subdivision_1, subdivision_2를 본다 |
| 6 | DDW에는 outlet이 없다. owner의 POC 개수와, 그 중 가지 않은 POC로 들어가는 것이 탐색이다. `poc_template_id = 0`은 사전 모델이 없는 날배관으로 보고 배관으로 쓴다. `owner_type = 256`은 의미가 불투명하므로 SQL과 판정에서 주석으로 남기고 보류한다 |
| 7 | 시험 도구는 설비 하나의 기본 추적이다. 이후에는 캐시를 읽어 같은 Tag가 시험 도구와 같은 결과가 되는지 설비마다 맞춘다 |
| 8 | DDW도 division 세 값으로 기본 분류가 된다. 노즐, 설비, 그 외를 이름과 이 세 값으로 본다 |
| 9 | 캐시 키는 Guid다. 위 1절의 로더 경계에서 맞춘다. 로직 분기는 엔진 복제가 아니라 규칙 객체다 |
| 10 | DDW 캐시는 S5D 캐시와 섞지 않는다. `x2_2`와 같이 별도 라인이고 이름은 `x2_m`이다. 폴더는 `GraphCache_x2_m`이다. `x1`, `x2`, `x2_2`에 쓰지 않는다 |
| 11 | Utility는 `tb_utilities`와 `tb_utility_groups`를 본다 |
| 12 | Information의 IN/OUT은 아직 열을 정하지 않는다. 확인 뒤에 로더에 넣는다 |

시험 도구에서 이미 맞은 DDW 걸음은 유지한다.

- 노즐은 `model_type` 50856202이거나, 이름에 `노즐`이 있고 `bim_yn = 1`일 때 멈춘다. damper `model_type` 51382287이 우선이다.
- Takeoff는 damper와 `pipe_type` 17301768의 연결이다. Takeoff는 객체가 아니라 그 damper의 connected poc다.
- tag는 `attribute_nm = tag`, connectiontag는 `ConnectionTag`, hookup number는 `S5_LATERAL_NUMBER`다. Takeoff와 노즐 모두 그 연결 POC id가 `tb_attributes.owner_id`다.
- 길이는 `length2`(mm)다. `length1`은 예비다.
- `model_type` 51380485는 설비 판정에서 빼고 통과한다. division 분류가 설비를 가리키면 그 판정이 우선인지 9절에서 확인한다.

## 3. 규칙 객체

공통으로 두는 것.

- 시작 설비의 모든 POC에서 걷는다. 방문한 POC로는 돌아가지 않는다.
- 배관 길이는 지난 배관의 `length2` 합이다.
- Takeoff를 만나면 Tag를 시작 설비 Tag와 비교한다. 일치, 포함, Damper 대괄호, 토큰 교집합은 지금 `EquipmentTagMatcher`를 쓴다.
- 시작 설비의 POC로 돌아오면 S5D와 같이 그 가지를 정상으로 끝낸다.
- 더 갈 POC가 없거나 owner를 못 찾거나 연결이 끊기면 `ing`다. 배관이 아닐 때도 같다. 시작 설비 POC에 연결 자체가 없을 때만 빈 끝(`emp`)이다.

`S5dRules`에만 두는 것.

- Cap은 지금 `IsCapName` 이름 목록이다.
- live는 `OperationType`.
- Water, MainLine continuation은 지금 엔진 그대로다.
- 설비 목록은 Sub1 `1-%`, `6-%`, Tag 있음, 건설코드 Delete 제외다.

`DdwRules`에 두는 것.

- Cap은 `bim_yn = 0`이다. S5D 이름 목록을 복사하지 않는다.
- 설비는 division, subdivision_1, subdivision_2가 S5D 설비 어휘일 때다. `model_type` 50594049만으로 설비를 만들지 않는다. 건설코드 Delete는 `Construction-code` 속성으로 제외한다.
- 노즐, Takeoff, 연결 POC 속성은 2절의 시험 도구 규칙이다.
- Water, MainLine은 끄고 둔다. Takeoff는 항상 기본 종료와 Tag 비교만 한다.
- 배관 owner는 `poc_template_id = 0`인 POC의 owner다. `owner_type = 256` 조건은 주석으로만 남긴다.

`bim_yn = 0`을 설비 조건으로 켜지 않는다. Cap 조건과 겹친다.

## 4. 로더

`DdwLoader`가 캐시에 넣는 값.

| 캐시 | DDW에서 읽는 곳 |
|---|---|
| 노드 id, POC id, 배관 id | `CONVERT(uniqueidentifier, …)` 또는 이미 uniqueidentifier인 열. 문자열 division id는 `TRY_CAST` |
| 노드 이름 | 템플릿 `display_nm` |
| division 세 글자 | 템플릿의 세 id를 `tb_division.division_value`로 |
| Tag | `tb_attributes`에서 `attribute_nm = tag` |
| 건설코드 | `attribute_nm = 'Construction-code'` |
| 노즐, Takeoff의 tag, connectiontag, hookup | 연결 POC id를 owner로. 이름은 `tag`, `ConnectionTag`, `S5_LATERAL_NUMBER` |
| 배관 | `poc_template_id = 0`인 POC의 owner, `length2`, `pipe_type` |
| Utility | POC의 utility id를 `tb_utilities`, 그 그룹을 `tb_utility_groups` |
| live | 조인하는 `tb_*`마다 `version_sequence < 1000000000` |

동적속성의 Hookup은 위 연결 POC의 `S5_LATERAL_NUMBER`로 채운다. 별도 Hookup 로그가 있다고 가정하지 않는다.

## 5. 캐시 라인

`TargetDatabases`에 `x2_m`을 둔다. 폴더는 공유 경로 아래 `GraphCache_x2_m`이다. `x2_2`가 별도 라인인 것과 같다. Admin만 `D:\PocAdmin`에서 쓴다. 암호는 그 폴더의 `config\db.secret`만 쓴다. UI, 철거, 노즐추적, Web은 그 캐시를 읽되 S5D 폴더와 파일을 섞지 않는다.

삭제 배관 도구는 S5D 전용으로 둔다. `x2_m`을 읽게 바꾸지 않는다.

## 6. 맞추는 순서

한 번에 운영 추적을 DDW로 바꾸지 않는다.

1. `DdwSql`, `DdwGraphMapping`, `DdwAcceptance`에서 `OPERATIONTYPE`을 제거하고 `version_sequence < 1000000000`으로 바꾼다. 이 사이트의 DDW 쿼리가 이 열 때문에 실패하므로 먼저 한다.
2. 같은 쿼리의 id를 Guid로 받는다. division 세 글자, `Construction-code`, `poc_template_id = 0` 배관을 노드와 배관에 채운다. `owner_type = 256` 필터는 주석이다.
3. `x2_m`에만 캐시를 쓴다.
4. 시험 도구에 넣었던 Tag 하나를 캐시로 다시 걷는다. 끝 status, 지난 배관의 `length2` 합, Takeoff와 노즐의 속성 owner가 시험 도구 Excel과 같은지 본다.
5. 맞는 설비가 늘어난 뒤에만 일배치 목록을 연다. Water, MainLine, Information IN/OUT은 그 뒤에 빈 자리에 넣는다.

시험 도구는 이 순서 동안 기준 결과로 남긴다. 캐시 쪽을 도구에 맞추고, 도구가 아직 없는 규칙(Cap, 비배관 `ing`, division 설비, 건설코드, 시작 설비 복귀, Takeoff Tag 비교)은 캐시 규칙과 도구에 같이 넣는다.

## 7. 코드에서 먼저 닫을 곳

| 위치 | 지금 | 설계 |
|---|---|---|
| `PocTrace.Core/Infrastructure/Data/DdwSql.cs` | `OPERATIONTYPE < 2`, 길이 `length1`, Tag 빈 값, 배관 끝 `owner_type` 256 | live는 `version_sequence`. 길이는 `length2`. Tag와 건설코드는 속성. 배관은 `poc_template_id = 0`. id는 uniqueidentifier |
| `DdwGraphMapping.cs` | `OperationType >= 2`를 버린다. 256을 배관 끝으로 본다 | 버린 행은 version 임계로 SQL에서 이미 빠진다. 256 분기는 주석 |
| `PocTrace.DdwAcceptance` | SQL에 `OPERATIONTYPE < 2`가 있는지 검사 | `version_sequence < 1000000000`을 검사 |
| `TraceAlgorithm` Cap, ING, Takeoff | S5D 이름 목록, 배관 끊김 위주, Water와 MainLine이 항상 켜짐 | 호출 전에 `DdwRules`를 고른다. DDW에서는 Water, MainLine을 호출하지 않는다 |
| 캐시 폴더 | 예전에 `ddw_x2m` 예시 | 라인 이름은 `x2_m` |

## 8. 확인이 남은 것

- `bim_yn = 0`인 객체 중 밸브, 피팅, 엘보처럼 통과해야 하는 것이 얼마나 되는지. 많으면 Cap 규칙이 첫 컴포넌트에서 추적을 끊는다. 한 설비 추적으로 건수를 보고, 그때까지 이 규칙을 운영 일배치에 올리지 않는다.
- division 세 글자가 S5D의 설비, `1-%`, `6-%`, MAIN, GCS와 같은 문자열인지. `TRY_CAST`가 실패한 템플릿 비율.
- `Construction-code`의 표기와 Delete를 뜻하는 값. 속성 이름 철자는 이 문서의 문자열을 그대로 조회한다.
- Information IN/OUT이 어느 테이블의 어느 열인지. 확인 전에는 로더가 빈 값으로 둔다.
- `tb_utilities`, `tb_utility_groups`의 이름 열과 POC의 utility id 열 이름.
- Takeoff Tag가 연결 POC의 `tag` 속성과 S5D Damper Tag로 같은 비교가 되는지. 첫 캐시 대조에서 불일치 건수만 본다.
- `poc_template_id = 0` 외에 빈 GUID인 POC도 날배관인지.
- 마스터(`tb_division`, `tb_utilities`)에도 `version_sequence < 1000000000`을 걸었을 때 행이 빠지는지.

## 9. 하지 않는 것

- DDW 캐시를 `x1`, `x2`, `x2_2`에 쓰지 않는다.
- Water, MainLine을 DDW에 미리 넣지 않는다.
- `owner_type = 256`으로 배관을 가르지 않는다.
- 시험 도구 한 설비 결과를 전체 라인의 운영 결과로 배포하지 않는다.
- Information IN/OUT 열을 추측해서 SELECT에 넣지 않는다.

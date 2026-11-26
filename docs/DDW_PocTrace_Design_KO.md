# DDW POC 추적 최초 설계

작성일: 2026-11-16. 시험 도구 `PocTrace.DdwTraceBench`로 한 설비를 따라본 결과와, 그 분석에 대한 답을 모은 설계다. 캐시 절차의 이전 기준은 `DDW_Cache_Strategy_KO.md`, Tag에서 배관까지의 조회 흐름은 `DDW_Trace_Flow_KO.md`다. 이 문서와 어긋나면 추적 규칙과 캐시 라인은 이 문서를 따른다.

2026-11-17에 `bim_yn` 규칙을 고쳤다. `bim_yn = 0`은 우리가 추적하는 2차 훅업 배관이다. 노즐과 덕트는 `bim_yn = 1`인 1차 배관이고 BIM을 통해 DDW에 들어온다. `bim_yn = 0`을 Cap으로 쓰지 않는다.

같은 날 타입 상수를 `DdwTypeCodes`에 두었다. 8자리이고 17로 시작하면 `pipe_type`, 5로 시작하면 `model_type`이다. 목록에 없는 값은 로그와 `GraphCache_x2_m/ddw-undefined-types.xlsx`에 남기고, 뜻을 받으면 상수에 추가한다. Admin의 DDW Cache 버튼이 `remote_monami.monami`를 읽어 `GraphCache_x2_m`만 쓴다. UI 콤보의 `x2_m`은 그 캐시를 읽는다. 캐시에는 2차 훅업만 담는다. `bim_yn = 1`인 1차 배관 부속과 건축 구조(`structure`, `architecture`, `beam`, `floor`, `wall`, `column`)는 넣지 않는다. 노즐과 damper는 추적 끝으로 남긴다. 덕트와 `ladder_cable_tray` 배관도 넣지 않는다. `family_key = equipment`이거나 설비 `model_type`이면 설비다. UI는 그 캐시를 공유 폴더의 `PocTrace.UI.exe`와 그 옆 `PocTrace.Core.dll`로 읽는다. Admin 폴더의 Core.dll만 갈아도 시작 판정은 그대로다. `x2_m`에서는 노드 종류가 설비이면 시작한다. Subdivision1이 `7-`으로 시작하는 퍼실리티도 설비로 캐시에 두고 추적 시작으로 본다. S5D처럼 `1-` `2-` `4-` `6-`일 필요는 없다. 설비 POC의 출구는 연결된 배관 id다. `UtMapping.csv`에 `x2_m`이 없으면 `x2` 매핑을 쓴다. Cap은 이름이나 division 세 칸에 `cap`이 있고 `equipment`가 아닐 때다. 기타, vac, flange END, 미사용 같은 S5D 목록은 DDW에 쓰지 않는다. Water와 MainLine은 아직 S5D 그대로다.

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

id 열은 `varbinary(16)`이다. 조회 결과는 `TRY_CONVERT(uniqueidentifier, …)`로 맞춘다. 이 변환은 `new Guid(byte[16])`과 같은 바이트 순서다. 캐시와 엔진은 `Guid`만 본다. 정수와 길이는 `int`, `float`로 맞춘다. `decimal`, `bigint`, `bit`을 그대로 넘기면 그 열에서 파싱 오류가 난다. `0x` 문자열은 시험 도구 Excel에만 남긴다. 템플릿의 division 세 열은 `TRY_CONVERT(열 AS uniqueidentifier)`로 `tb_division`에 맞추고, 노드에는 `division_value` 세 글자를 붙인다. 글자가 빈 노드는 Sub 어휘 판정이 같이 빈다.

## 2. 확정

| 번호 | 결정 |
|---|---|
| 1 | DDW 조회에서 `OPERATIONTYPE`을 모두 뺀다. 그 열은 DDW에 없다. live는 `version_sequence < 1000000000`이다. 모델, POC, 배관, 속성, 템플릿, division, 유틸리티 조인에 같은 조건을 건다. 한 SQL에 두 열을 섞지 않는다 |
| 2 | 특수 판정은 스키마를 가른다. S5D Cap은 지금 이름 목록이다. DDW에서 `bim_yn = 0`은 추적 대상인 2차 훅업이고, `bim_yn = 1`은 BIM에서 온 노즐과 덕트다. Cap이 아니다. 객체 분류는 이름과 division 세 값을 본다 |
| 3 | Water Takeoff, MainLine Takeoff는 DDW에서 보류다. 자리가 필요해지면 `DdwRules`에 추가한다. 그 전까지 DDW Takeoff는 멈추는 기본 경로만 탄다 |
| 4 | Takeoff Tag와 시작 설비 Tag 비교는 S5D와 같다. 3번의 특수 경로는 빼도 기본 비교는 같다. 시작 설비로 돌아오면 S5D와 같이 처리한다. 배관이 아닌 객체에서 더 갈 수 없으면 `ing`다 |
| 5 | 건설코드는 `tb_attributes.attribute_nm = 'Construction-code'`다. 그 값에 delete 가 있으면 철거 대상이다. DDW는 초기 설계라 당분간 철거 설비는 없고 신규 반입만 있다. 설비 구분은 S5D처럼 division, subdivision_1, subdivision_2를 본다. DDW division 은 x2를 복사한 것이라 `1-%` 같은 어휘를 그대로 쓴다 |
| 6 | DDW에는 outlet이 없다. owner의 POC 개수와, 그 중 가지 않은 POC로 들어가는 것이 탐색이다. `poc_template_id`가 0인 것만 날배관이다. 이 기준은 바뀔 수 있다. `owner_type = 256`은 의미가 불투명하므로 SQL과 판정에서 주석으로 남기고 보류한다 |
| 7 | 시험 도구는 설비 하나의 기본 추적이다. 이후에는 캐시를 읽어 같은 Tag가 시험 도구와 같은 결과가 되는지 설비마다 맞춘다 |
| 8 | DDW도 division 세 값으로 기본 분류가 된다. 노즐, 설비, 그 외를 이름과 이 세 값으로 본다 |
| 9 | 캐시 키는 Guid다. 위 1절의 로더 경계에서 맞춘다. 로직 분기는 엔진 복제가 아니라 규칙 객체다 |
| 10 | DDW 캐시는 S5D 캐시와 섞지 않는다. `x2_2`와 같이 별도 라인이고 이름은 `x2_m`이다. 폴더는 `GraphCache_x2_m`이다. `x1`, `x2`, `x2_2`에 쓰지 않는다 |
| 11 | Utility는 `tb_utilities.utility_nm` 이고, 그룹은 `tb_utility_groups.utility_group_nm` 이다. POC의 utility id가 `utility_id`로 연결되고, 유틸리티의 `utility_group_id`가 그룹으로 연결된다. division, utilities, utility_groups 모두 `version_sequence`가 있다 |
| 12 | GCS 설비의 Information은 POC 이름을 본다. 이름에 `_in_`이 있으면 IN이고, `stick_` 다음에 숫자가 있으면 OUT이다. 메모가 있으면 메모를 유지한다 |

시험 도구에서 이미 맞은 DDW 걸음은 유지한다.

- 노즐은 `model_type` 50856202이거나, 이름에 `노즐`이 있고 `bim_yn = 1`일 때 멈춘다. damper `model_type` 51382287이 우선이다.
- Takeoff는 damper와 `pipe_type` 17301768의 연결이다. Takeoff는 객체가 아니라 그 damper의 connected poc다. DDW Takeoff의 tag 속성은 S5D damper tag와 같다.
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

- Cap은 `IsCapName` 이름 목록이다. 기타, Valve Box, vac, bulk, nw25 pot, flange END, 미사용이 여기 있다.
- live는 `OperationType`.
- Water, MainLine continuation은 지금 엔진 그대로다.
- 설비 목록은 Sub1 `1-%`, `6-%`, Tag 있음, 건설코드 Delete 제외다.

`DdwRules`에 두는 것.

- Cap은 이름, division, subdivision_1, subdivision_2 중 하나에 `cap`이 있고, 그 세 칸에 `equipment`가 없을 때다. S5D Cap 목록과 ING를 Cap으로 바꾸는 보정은 쓰지 않는다.
- `bim_yn = 0`은 추적하는 2차 훅업 배관이다. `bim_yn = 1`은 BIM에서 들어온 노즐과 덕트다. 이 값을 Cap이나 설비 스위치로 쓰지 않는다.
- 설비는 division, subdivision_1, subdivision_2가 S5D 설비 어휘일 때다. `model_type` 50594049만으로 설비를 만들지 않는다. 건설코드에 delete 글자가 있으면 철거로 보고 제외한다. 당분간 그 행은 없을 것으로 본다.
- 노즐, Takeoff, 연결 POC 속성은 2절의 시험 도구 규칙이다.
- Water, MainLine은 끄고 둔다. Takeoff는 항상 기본 종료와 Tag 비교만 한다.
- 배관 owner는 `poc_template_id = 0`인 POC의 owner다. `owner_type = 256` 조건은 주석으로만 남긴다.

`bim_yn = 0`을 설비 조건으로 켜지 않는다. 그 값은 추적 대상 전체다.

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
| 배관 | `poc_template_id = 0`인 POC의 owner, `length2`, `pipe_type`, `utility_id`. 작업시각과 작업자는 `version_sequence = tb_versions.version_number`로 `user_id`, `last_update`를 읽고, `tb_new_users`에서 `login_id`, `user_name`, `company_name`을 읽는다. 배관 테이블의 `user_id`는 쓰지 않는다 |
| Utility | POC의 utility id를 `tb_utilities`, 그 그룹을 `tb_utility_groups` |
| live | 조인하는 `tb_*`마다 `version_sequence < 1000000000` |

동적속성의 Hookup은 위 연결 POC의 `S5_LATERAL_NUMBER`로 채운다. 별도 Hookup 로그가 있다고 가정하지 않는다.

## 5. 캐시 라인

`TargetDatabases`에 `x2_m`을 둔다. 폴더는 공유 경로 아래 `GraphCache_x2_m`이다. `x2_2`가 별도 라인인 것과 같다. Admin만 `D:\PocAdmin`에서 쓴다. 암호는 그 폴더의 `config\db.secret`만 쓴다. UI, 철거, 노즐추적, Web은 그 캐시를 읽되 S5D 폴더와 파일을 섞지 않는다.

삭제 배관 도구는 S5D 전용으로 둔다. `x2_m`을 읽게 바꾸지 않는다.

## 6. 맞추는 순서

한 번에 운영 추적을 DDW로 바꾸지 않는다.

1. `DdwSql`의 live 조건은 `version_sequence < 1000000000`이다. 배관 POC는 `poc_template_id = 0`만 보고 길이는 `length2`다.
2. 노드 Tag와 건설코드, POC의 tag와 ConnectionTag, hookup은 `tb_attributes`에서 채운다. `owner_type = 256` 필터는 주석이다.
3. Admin의 DDW Cache 버튼이 `x2_m`에만 캐시를 쓴다. 09~13 조회는 2026-11-17에 SVDI에서 오류 없이 끝났다. Cache Only와 전체 배치는 S5D라 `x2_m`을 건너뛴다. UI 콤보는 그 캐시만 읽는다.
4. 시험 도구에 넣었던 Tag 하나를 캐시로 다시 걷는다. 끝 status, 지난 배관의 `length2` 합, Takeoff와 노즐의 속성 owner가 시험 도구 Excel과 같은지 본다.
5. 맞는 설비가 늘어난 뒤에만 일배치 목록을 연다. Water와 MainLine은 그 뒤에 빈 자리에 넣는다. S와 T의 Connector Information은 `poctemplates.memo`다. PocNo는 `connection_order`다.

시험 도구는 이 순서 동안 기준 결과로 남긴다. 캐시 쪽을 도구에 맞추고, 도구가 아직 없는 규칙(Cap, 비배관 `ing`, division 설비, 건설코드, 시작 설비 복귀, Takeoff Tag 비교)은 캐시 규칙과 도구에 같이 넣는다.

## 7. 코드에서 먼저 닫을 곳

| 위치 | 지금 | 설계 |
|---|---|---|
| `PocTrace.Core/Infrastructure/Data/DdwSql.cs` | live는 `version_sequence`. 길이는 `length2`. 배관은 `poc_template_id = 0` | 추적 엔진의 Cap과 Water는 아직 이 쿼리를 규칙으로 쓰지 않는다 |
| `DdwGraphMapping.cs` | 배관 끝은 배관 id 집합으로 본다. Tag와 건설코드는 속성 | `bim_yn`은 캐시 필드가 아니다. 규칙은 이 문서에만 있다 |
| `TraceAlgorithm` Cap, ING, Takeoff | DDW Cap은 이름과 division의 `cap`만 본다. Water와 MainLine은 S5D만 탄다. DDW Takeoff POC는 TOF로 끝낸다 | DDW에서는 Water, MainLine을 호출하지 않는다 |
| 캐시 폴더 | 예전에 `ddw_x2m` 예시 | 라인 이름은 `x2_m` |

## 8. 확인이 남은 것

- division 세 글자가 S5D의 `1-%`, `6-%`와 같은지. DDW division은 x2 복사본이므로 같은 어휘로 본다. `TRY_CAST`가 실패한 템플릿 비율은 한 설비 캐시에서 확인한다.
- 건설코드 속성에 delete가 들어 있는 철거 설비가 실제로 있는지. 초기 설계 동안은 없을 것으로 본다.
- S: Connector Information과 T: Connector Information은 `poctemplates.memo`만 쓴다. 이름이 `_in_`이거나 `stick_` 뒤가 숫자여도 memo가 비면 칸은 빈다.
- `x2_m`의 S: Instance ID는 소유 객체의 DDW id다. S5D 해시 함수를 쓰지 않는다. 노즐 Information에 `Nozzle`이 없어도 노즐 노드면 NOZ로 끝낸다.
- UI 버전은 `1.001` 형식이고 컴파일마다 0.001씩 오른다. 빌드가 최신으로 보여도 버전 스탬프는 다시 실행된다. 폼 하단과 추적 엑셀 Summary의 V1에 적는다. Summary E1 헤더는 `eqp count`다. 이름 표는 B열부터 쓴다. 1행과 2행의 요약 표는 A열에 둔다. 미배치설비는 배치 요약에만 두고 개별 엑셀은 만들지 않는다. BOM은 설비 Tag, UT group, 사용 횟수이고 배관 길이는 설비 Tag와 UT group으로만 묶는다. 모델 좌표 `posx`, `posy`, `posz`는 소수 1자리로 캐시 v14에 둔다. POC 좌표는 `tb_pocinstances`의 같은 세 값을 캐시 v15에 둔다. pump의 fore-line POC와 middle foreline 피팅의 X, Y 1:1은 `docs/MiddleForelineAlign.sql`이 좌표 묶음으로 판정한다. 이 값은 객체 삽입점이다. 배관 중심선과 고체 간섭은 아니다. 설비 기준 상대좌표 스케치와 끝점 주변 점 개수는 가능하고, 그림은 엑셀 도형보다 HTML SVG가 맞다. HTML 생성을 켜면 Summary를 만들고 Drawing은 x2_m에서만 만든다. 다른 라인은 Drawing을 만들지 않는다. x2_m 배치 업로드는 eq code 외에 大分类, 小分类, 건설CODE, 设计社, 设备搬入日, 出图需求日를 헤더 위치와 상관없이 읽어 Summary에 넣는다. EQ Code는 건설CODE와 设计社 사이에 둔다. Result가 FAIL이면 그 글자와 Issues 숫자를 적색으로 하고, 업로드 건설CODE가 캐시 설비 ConstructionCode와 다르면 비고에 적는다. Poc, BOM, Pipe는 HTML로 만들지 않는다. 그림은 삽입점 3차원이다. HTML은 마우스로 돌리고 엑셀 Drawing은 등각 그림이다. 같은 UT는 같은 색이고 다른 UT는 다른 색이다. UT 이름은 연결된 같은 배관 중 가장 긴 곳에 한 번만 적는다. 끝점 글자는 NOZ, TOF, CAP, EQP를 녹색, ING, ERR을 적색, EMP를 검정으로 하고 EMP는 설비 이름과 떨어뜨린다. Drawing 오른쪽에 설비 POC별 status 표가 있다. 출발점에는 설비 이름을 한 번만 적고, 끝점 TOF, NOZ는 좌표가 없는 takeoff에도 마지막 점에 적는다. FIT 글자는 넣지 않는다. Web 결과 표는 Tag당 한 행이다. Admin report의 오류 원인 귀속 원 그래프 자리에는 점유율 상위 2개 오류의 날짜별 추이를 둔다. 미설계 배치 설비는 배치 엑셀과 Drawing에서 뺀다. Pipe 시트의 S: Instance Id와 S: Model Name은 그 행 시작 POC의 owner와 이름이다. UI 배치는 설비별 엑셀과 Summary.xlsx를 한 zip으로 묶는다. Web 화면은 exe 옆 wwwroot가 필요하다. 검수 기록 삭제는 시각 문자열을 같은 형식으로 맞춰 표식이 행을 찾게 한다. `x2_m` 엑셀에는 `추적` 시트를 둔다. hookup number는 attribute `HookupNumber`이고, pipe number는 `S5_LATERAL_NUMBER`다. 배관 구경은 `tb_pipeinstances.diameter1`이다. `pipesize_id`는 이 테이블에 없다. `x2_m` EMP의 분기배관수는 0이다. POC Utility가 비어 있으면 설비정합오류이고 이슈 오너는 ft 기술이다. Web의 라인 목록에 `x2_m`이 있다. UI와 Web 배치 Excel은 첫 행의 `eq code` 열을 Tag로 읽는다. Tag가 없으면 미배치설비, Tag는 있으나 연결 배관이 없으면 미설계 배치 설비로 추적 행을 남긴다. 추적 시트에서 tag와 connection tag는 POC id이고, pipe number는 객체 id의 `S5_LATERAL_NUMBER`다. Takeoff는 그 POC의 connected poc를 한 번만 따라가 owner인 duct pipe에서 pipe number를 읽는다. `pipe_type` 17303552 duct 객체는 그래프에 넣지 않고, 그 id의 `S5_LATERAL_NUMBER` 속성만 남긴다. Details T:Tag는 takeoff POC의 tag를 `Damper[tag]`로 적는다. 덕트 쪽 POC에 tag가 없으면 같은 takeoff의 다른 POC에서 가져온다.
- Takeoff tag 속성이 S5D damper tag와 같은 비교가 되는지는 한 설비 대조에서 건수를 본다.
- `poc_template_id = 0`만 날배관이다. 다른 값이 날배관으로 보이면 상수 기준을 바꾼다.
- 정의되지 않은 `pipe_type`, `model_type`은 캐시 폴더의 `ddw-undefined-types.xlsx`에 쌓인다. 뜻을 받으면 `DdwTypeCodes`에 추가한다.

## 9. 하지 않는 것

- DDW 캐시를 `x1`, `x2`, `x2_2`에 쓰지 않는다.
- Water, MainLine을 DDW에 미리 넣지 않는다.
- `owner_type = 256`으로 배관을 가르지 않는다.
- 시험 도구 한 설비 결과를 전체 라인의 운영 결과로 배포하지 않는다.
- Information IN/OUT을 테이블 열로 추측해서 SELECT에 넣지 않는다. POC 이름 규칙만 쓴다.

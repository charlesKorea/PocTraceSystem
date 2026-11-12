# DDW 설비 Tag 배관 추적 flow

이 문서는 SVDI 담당자가 실제 DDW 솔루션과 `monami` 테이블에서 손으로 따라간 추적(2026-11-12)을 정리한다. 캐시 전략은 `DDW_Cache_Strategy_KO.md`, 조회 결과 기록은 `DDW_SVDI_Results_KO.md`가 맡는다. 이 문서는 "DDW에서 Tag 하나가 배관 길이까지 어떻게 이어지는가"와 그에 따른 설계 차이, 아직 모르는 것을 모은다.

사례의 Tag, 모델 이름, GUID 전문은 이 문서에 적지 않는다. 단계 설명에는 `<TAG>`, `<EQ>`처럼 자리표시자를 쓴다.

## 1. 전제

- DDW 테이블은 `remote_monami.monami.dbo.tb_*`이다. 이 문서와 대화에서 `*`만 적으면 그 테이블이다(예: `pocinstances`).
- 모든 테이블 조회에 `version_sequence < 1000000000`이 기본으로 붙는다. 그 이상인 행은 삭제 행이라 제외한다(담당자 확정 2026-11-12). 앞서 들은 "10000000 이상"은 이 값으로 정리한다.
- DDW에는 `operationtype`이 없다. S5D의 OperationType 0/1/2는 DDW에서 `version_sequence`가 대신한다.
- 모든 속성은 `attributes` 한 테이블(EAV)에 있다. 설비 Tag도 모델 테이블이 아니라 여기에 있다.
- 길이 단위는 mm이고 변환하지 않는다(`pipeinstances.length2`, 담당자 설명).

## 2. 테이블 지도

| 테이블 | 키와 연결 열 | 이 flow에서 하는 일 |
|---|---|---|
| `attributes` | `owner_id`, `attribute_nm`, `attribute_val`, `owner_type` | `attribute_nm = 'tag'`와 Tag 값으로 설비 인스턴스 id를 찾는다 |
| `modelinstances` | `instance_id`, `model_type`, `model_template_id` | 배치된 객체. `model_type` 50594049이면 설비, 51380485이면 connector_pipe |
| `modeltemplates` | `model_template_id`, `display_nm`, `family_key`, `bim_yn` | 라이브러리 모델. 이름, `family_key = equipment` 힌트, `bim_yn = 0`이면 2차 설계 객체 |
| `poctemplates` | `model_template_id`, `poc_template_id`, `poc_template_nm`, `utility_id`, `equipment_yn`, `memo` | 라이브러리 POC. `equipment_yn = 1`이면 설비 POC, `memo`는 비고 |
| `pocinstances` | `instance_id`, `owner_instance_id`, `poc_template_id`, `utility_id`, `connected_poc_id` | 배치된 POC. `connected_poc_id`가 S5D의 outlet. 상대 POC의 `instance_id` |
| `pipeinstances` | `instance_id`, `length2`, 구경 열 | 배관 자체. `pocinstances.owner_instance_id`로 닿는다 |

## 3. 추적 단계

각 단계의 쿼리는 `ddw-svdi-queries/T01-flow-steps.sql`에 있다.

1. **Tag로 설비 id를 찾는다.** `attributes`에서 `attribute_nm = 'tag'`이고 `attribute_val = <TAG>`인 행의 `owner_id`가 그 속성을 가진 객체(`<EQ>`)다.
2. **설비인지 본다.** `modelinstances`에서 `instance_id = <EQ>`. `model_type = 50594049`이면 설비로 본다. 같은 행의 `model_template_id`는 배치 전 라이브러리 모델의 id다.
3. **모델 이름과 힌트.** `modeltemplates`에서 `model_template_id`로 `display_nm`(DDW 화면의 객체 이름과 같다)을 본다. `family_key = equipment`는 설비라는 두 번째 힌트다. `bim_yn = 0`은 1차 BIM 설계 객체가 아닌 2차 설계 객체라는 뜻이다.
4. **라이브러리 POC.** `poctemplates`에서 `model_template_id`로 그 모델의 POC 목록을 얻는다. `poc_template_nm`이 POC 이름, `poc_template_id`가 POC 고유 id, `utility_id`가 Utility id다. `equipment_yn = 1`이면 설비의 POC이고 `memo`는 비고 문자열이다.
5. **배치된 POC.** `pocinstances`에서 `poc_template_id`와 설비 id(`owner_instance_id = <EQ>`)를 같이 건다. 설비 id를 안 걸면 같은 모델의 다른 설비 POC가 섞인다. 이 행에서 `instance_id`(이 POC), `utility_id`, `poc_template_id`, `owner_instance_id`, 가장 중요한 `connected_poc_id`(건너편 POC)를 얻는다.
6. **건너편 POC.** `pocinstances`에서 `instance_id = connected_poc_id`로 조회하면 그 POC의 `poc_template_id`, `owner_instance_id`(건너편 소유 객체), `connected_poc_id`를 얻는다. 이 `connected_poc_id`는 방금 건너온 POC를 되가리킨다. S5D처럼 지나온 POC id로는 가지 않는다.
7. **건너편 소유 객체.** `modelinstances`에서 `instance_id = 건너편 owner_instance_id`로 찾는다. `model_type = 51380485`이면 connector_pipe다(동료의 핸드오프 자료 로직에도 같은 값이 있다). 그 객체를 소유자로 하는 POC를 `pocinstances`에서 `owner_instance_id`로 다시 모두 읽으면 2개 이상이 나온다. 그중 아직 지나지 않은 POC의 `connected_poc_id`로 추적을 잇는다.
8. **배관을 만나면.** 추적 중 `poc_template_id`가 0(전부 0인 id)인 POC를 만나면 그 POC의 `owner_instance_id`로 `pipeinstances`에서 `instance_id`를 찾는다. `length2`가 배관 길이(mm)이고 구경 열(예: 값 19)에서 배관 크기를 얻는다.
9. **되풀이.** 1~8을 건너편 POC마다 반복해, 주어진 Tag 설비의 POC에서 배관과 컴포넌트 쪽으로 퍼져 나가는 것이 기본 동작이다.

### 의사코드

```text
equip   = attributes('tag' = <TAG>) 의 owner 중, modelinstances.model_type = 50594049 인 것
visited = {}   // POC instance_id
for E in equip:
  queue = pocinstances(owner_instance_id = E)
  visited += queue
  while queue:
    P = pop(queue)
    Q = P.connected_poc_id
    if Q is null/zero or Q not live: 끝 (미연결 또는 dangling)
    if Q in visited: continue
    visited += Q
    T = pocinstances(instance_id = Q)
    if T.poc_template_id == 0:
        pipe = pipeinstances(instance_id = T.owner_instance_id)    // length2, 구경
        기록(pipe)
        queue += pocinstances(owner_instance_id = pipe.id) - visited   // 배관의 다른 끝점
    else:
        owner = modelinstances(instance_id = T.owner_instance_id)
        if owner 가 설비/노즐/캡 같은 종료 대상: 멈춤        // 종료 조건은 5절 질문
        else:  // connector_pipe, 피팅 등
            기록(owner)
            queue += pocinstances(owner_instance_id = owner.id) - visited
```

모든 조회에 `version_sequence < 1000000000`이 붙는다. 한 객체의 POC가 셋 이상이면(분기) 아직 안 간 POC마다 가지를 이어 간다.

## 4. S5D와 현재 코드와의 비교

| 항목 | S5D | 현재 DDW 코드 | 이 flow가 말하는 것 |
|---|---|---|---|
| 삭제 판정 | `OperationType` 0/1/2, 2가 삭제 | 모든 SELECT가 `OPERATIONTYPE < 2` | 이 DB에 열이 없다. `version_sequence < 1000000000`으로 바꾼다 |
| 설비 Tag | 설비 로그 열 | TbStar는 `NULL`, WebDdw는 `mi.[tag]` 열 | Tag는 `attributes`의 `tag` 행이다. 둘 다 아니다. 속성 읽기를 추가해야 한다 |
| 설비 판정 | Division/Sub 어휘, 건설코드 등 | `MODEL_TYPE` 50594049/50594048만으로 설비를 만들지 않는다(Grating이 섞인다고 가정) | 담당자는 `model_type = 50594049`와 `family_key = equipment`로 설비를 본다. 두 가정이 충돌한다. 질문 Q-03 |
| 배관 길이 | `PipeLog`의 길이 | `length1`을 `NormalizeLength`로 읽는다 | 담당자는 `length2`(mm)라고 한다. 질문 Q-01 |
| 배관 끝점 | 배관 Outlet1~3 | `owner_type = 256`인 POC가 배관 끝점 | 담당자는 `poc_template_id = 0`으로 배관을 알아본다. 두 기준이 같은 집합인지 질문 Q-06 |
| connector_pipe | 서브디비전 이름으로 분류 | 코드에 51380485가 없다 | 설비-배관 사이에 낀 요소다. 길이를 어떻게 셀지 질문 Q-07 |
| 되돌아가지 않기 | outlet 방문 집합 | 그래프 순회가 방문 집합을 쓴다 | 같은 규칙이다 |
| 시작 POC | 설비의 POC 전체 | `FindStartPocs`가 Tag로 노드를 찾고 노드의 POC에서 출발 | 코드의 시작 방식은 같다. Tag 원천만 다르다 |

## 5. 아직 모르는 것

번호 Q-01~Q-12는 `DDW_SVDI_Results_KO.md` 6절과 같다. 괄호는 답을 줄 쿼리다.

| 번호 | 질문 | 답을 줄 쿼리 |
|---|---|---|
| Q-01 | 배관 길이는 `length2`인가 `length1`인가. 두 값이 다른 배관은 얼마나 되고, 합계가 얼마나 다른가 | `T05-A`, `T05-B` |
| Q-02 | 구경 열 이름은 무엇이고, POC 구경(`poctemplates`, `pocinstances`)과 배관 구경은 어떻게 연결되는가. `pipesize` 테이블은 있는가 | `03`, `04`, `08` 보류 해제 |
| Q-03 | 설비 판정: `model_type` 50594049, 50594048, `family_key = equipment`, `bim_yn` 중 무엇이 설비의 필요충분 조건인가. 설비가 아닌데 50594049이거나, 설비인데 아닌 것이 있는가 | `T03-A`~`T03-C` |
| Q-04 | Tag: `attribute_nm`의 이름 변형과 `owner_type`, 한 Tag가 여러 owner에 붙는 정도, 한 owner에 tag 행이 여럿인 경우. 1차 BIM(`bim_yn = 1`) 설비의 Tag는 어느 속성에 있는가 | `T02-A`~`T02-D`, 1차 BIM은 `SLOW-07` 결과 후 |
| Q-05 | 시작 POC 범위: 설비 인스턴스의 POC 전부인가 `equipment_yn = 1`만인가 | `T01` 4, 5단계를 설비 몇 개에 돌려 비교 |
| Q-06 | `poc_template_id = 0`인 POC와 `owner_type = 256`인 POC는 같은 집합인가. 배관 하나의 POC 개수 분포 | `T04-A`, `T04-C` |
| Q-07 | connector_pipe 하나가 POC를 몇 개 갖는가. 길이가 있는가(`pipeinstances`에 행이 있는가, 아니면 다른 방법인가). 그 밖에 추적 중 만나는 `model_type`은 무엇이 있는가(피팅, 밸브, 캡, 노즐) | `T04-B`, `T03-B`, `SLOW-06` |
| Q-08 | 종료 조건: 다른 설비에서 멈추는가. 노즐, 캡, Takeoff(`pipe_type = 17301768`)는 DDW에서 무엇으로 식별하는가. S5D의 "다른 설비/노즐/캡/Takeoff에서 멈춘다"를 그대로 옮길 수 있는가 | `SLOW-08`, `SLOW-02`, 대표 설비 1건을 T01로 끝까지 따라가 만나는 `model_type`을 기록 |
| Q-09 | `connected_poc_id`가 서로를 가리키는가(대칭), 상대가 없거나 삭제된 경우(dangling)는 얼마나 되는가 | `T04-D`, `SLOW-04` |
| Q-10 | 한 `instance_id`에 행이 여러 개인가. 삭제는 새 행인가 제자리 갱신인가. 이력 행이 있다면 live 행을 어떻게 고르는가 | `T06` |
| Q-11 | 삭제 행(`version_sequence >= 1000000000`)이 얼마나 남아 있고, 언제 삭제됐는지(`last_update`) 알 수 있는가. 삭제 배관 도구를 DDW로 옮길 수 있는가의 전제 | `T05-C`, `T05-D`, `SLOW-01` |
| Q-12 | `poctemplates.memo`를 보고서의 비고(Information)에 쓰는가. 마스터 테이블(`modeltemplates`, `poctemplates`, `utilities`)의 삭제 행도 같은 임계로 거르는가 | 담당자 답변, `SLOW-01`은 마스터를 보지 않는다 |

### 확인 순서 제안

1. `03`, `04`로 열 명세를 먼저 받는다(구경 열, `length2`, `owner_instance_id`, `last_update` 등 이 문서가 가정한 열 이름을 확인한다). T 쿼리가 열 이름 때문에 에러가 나면 이 둘을 먼저 본다.
2. `T01`을 대표 설비 1건으로 단계별로 돌려, 이 문서 3절 flow와 DDW 화면 결과를 맞춘다.
3. `T02`~`T06`을 돌린다.
4. `SLOW-01`은 임계가 확정되어 분포 확인용으로 내려갔다.

### 5.1 담당자 답변 (2026-11-12)

| 번호 | 답변 |
|---|---|
| Q-01 | 길이는 `length2`. `length1`은 예비로 같이 담는다 |
| Q-02 | 구경 열은 `pipesize_id`. `tb_pipesize.pipesize_id`와 맞고 `pipesize_nm`에 "14.29 mm" 같은 text가 있다 |
| Q-03 | 설비 판정은 `model_type`, `family_key`, `bim_yn` 세 가지를 모두 참조하되 하나만 만족해도 설비. 향후 변경 가능 |
| Q-04 | Tag뿐 아니라 모든 속성은 `tb_attributes`의 `attribute_nm`과 값 열로 찾는다. 1차 BIM 설비도 같다 |
| Q-05 | 시작 POC만이 아니라 그 POC의 주인 설비가 가진 모든 POC가 추적 대상 |
| Q-06 | 다를 것 같다. `owner_type = 256`은 현재 불분명하며 향후 시험으로 재확인 |
| Q-07 | `model_type`은 참고만 한다. 대신 기록해 다수 데이터로 유추한다 |
| Q-08 | 다른 설비로 가거나 `modeltemplates.bim_yn = 1`인 객체를 만나면 그 이름을 기록하고 멈춘다. 상세 status는 구분하지 않고 추적이 올바른지에 집중한다 |
| Q-09 | 없는 경우는 없는 것으로 두고 비고(status)로 남긴다 |
| Q-10 | `instance_id`는 삭제 행을 제외하면 유일하다고 간주한다 |
| Q-11 | 삭제 행이 남아 있다. `version_sequence`로 알고 `last_update` 열로 시각을 식별한다 |
| Q-12 | `memo`는 당장 추적에 필요하지 않아 보이지만 기록은 남긴다 |

이 답변으로 3절 flow는 두 가지가 바뀐다. 5단계의 시작 POC가 설비의 모든 POC로 넓어졌다. 8단계의 길이 열이 `length2`로 정해졌다. 의사코드의 종료 조건은 "다른 설비 또는 `bim_yn = 1` 객체"다.

## 6. 시험 도구 `PocTrace.DdwTraceBench`

SVDI에서 위 규칙이 맞는지 보기 위한 WinForms 도구다(`PocTrace.DdwTraceBench/`, 사용법은 그 폴더의 `가이드.md`). Tag를 입력(기본 `rme010n03`)하면 설비의 모든 POC에서 배관을 따라가고, `bim_yn = 1` 객체나 다른 설비를 만나면 이름을 기록하고 그 가지를 멈춘다. 지나간 POC, 배관, 객체의 id와 table, `length2`와 `length1`, 구경 이름, `model_type`, `family_key`, `bim_yn`, `memo`를 Excel에 쓴다.

도구의 해석 두 가지는 답변에 없어서 정한 것이다.

- connector_pipe(`model_type` 51380485)는 설비 판정에서 빼고 통과한다. 설비-배관 사이에 낀 요소로 읽었다. 화면에서 바꿀 수 있다.
- 설비 판정의 `bim_yn = 0` 조건은 기본 꺼짐이다. 켜면 2차 설계 객체(`bim_yn = 0`)인 밸브와 피팅이 설비가 되어 첫 컴포넌트에서 멈추기 쉽다. 객체 시트에 세 판정 결과가 모두 남으므로 시험 후 켤지 정한다.

도구는 `tb_*`를 키 목록 `IN`으로 200개씩 읽고 원격 조인을 하지 않는다. 열 이름이 다르면 `연결·스키마 확인`이 필요한 열을 알려 준다.

## 7. 코드에 닿는 변경 후보 (아직 고치지 않음)

- live 문: `version_sequence < 1000000000`을 `DdwSql`의 모델, POC, 배관, 속성, 마스터 조인 전부에 건다. `DdwGraphMapping`의 `OperationType >= 2` 판정과 `DdwAcceptance` 검사 문자열도 바꾼다.
- Tag: `attributes`에서 `attribute_nm = 'tag'`를 읽어 노드 Tag에 채운다. 큰 테이블(약 1,195만 행)이므로 이름 목록 `IN` 한 번으로 읽고 OWNER별 상관 서브쿼리를 만들지 않는다.
- 길이: `length2`(확정). `length1`은 예비 값으로 DTO에 같이 담는다.
- 설비 판정과 connector_pipe 분류: 시험 도구 결과(객체 시트의 `model_type` 분포)를 본 뒤 `DdwGraphMapping.Classify`에 반영한다.

이 항목들은 `DDW_Cache_Strategy_KO.md`의 C0 방침(이 단계에서는 C# 변경 없음)에 따라 쿼리 결과가 모일 때까지 보류한다.

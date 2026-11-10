# DDW DB 캐시 생성 전략

작성일: 2026-11-10. 기준 코드는 PocTrace `main` `9cc3202`이다. 이 문서는 캐시를 어떻게 만들지 정한다. 이 단계에서는 C#을 바꾸지 않는다.

독자는 Admin으로 공유 캐시를 만드는 쪽이다. Viewer, Web, 철거, 노즐추적은 그 캐시만 읽는다. DDW를 읽어도 지금 S5D 캐시와 같은 파일 종류, 같은 DTO, 같은 추적 규칙이 나와야 한다.

## 1. 자료와 우선순위

| 순서 | 자료 | 여기서 쓰는 이유 |
|---|---|---|
| 1 | 이 문서 | 캐시 생성의 현재 기준. 아래 자료와 어긋나면 캐시 절차는 이 문서를 따른다. |
| 2 | 현재 PocTrace 코드 | `SchemaKind`, `DdwSql`, `DdwGraphQueryProvider`, `DdwGraphMapping`, `DailyTraceJob`, `BinaryCacheRepository`. 2026-10 핸드오프가 "코드를 아직 안 고쳤다"고 적은 부분은 이미 지나갔다. |
| 3 | `D:\JCS\1. Codes\260718\CursorHandoff` | `DDW_INTEGRATED_DESIGN.md`, `LogicalContract.json` 1.2.0, `ddw-design-package/`. 매핑·live·끝점·금지 규칙의 계약. |
| 4 | `D:\JCS\1. Codes\260718\JSearchMK\1. Latest Source Code` | FRM에서 DDW형 `tb_*`를 **원격**으로 읽는 운영 쿼리. `FrmDbClient`, `FrmMonamiQueryBuilder`, `docs/Verify.X2M.monami.ko.md`, `docs/FiveDHistory.ko.md`. |
| 5 | `docs/BinaryCache_Easy_Guide_KO.md`, `docs/SVDI-SHARED-RUN.md` | 캐시 폴더의 업무 의미와 실행 위치. |

계약 1.2.0 changelog의 "캐시 발행은 `SqlQueries`를 직접 호출한다"는 문장은 지금 코드와 다르다. 그래프 묶음은 `GraphQueryProviders.CreateFromConfig().LoadAsync`가 읽는다. sidecar와 전체 설비 목록만 아직 `SqlQueries`의 S5D 문장이다.

동료 Java SQL과 JSearchMK의 노즐 쿼리는 참고다. 캐시 SQL로 복사하지 않는다. 샘플 행의 GUID, Tag, 좌표, 회사명은 이 문서에 넣지 않는다. 접속 계정과 주소도 넣지 않는다.

## 2. 한 줄 결론

DDW 캐시는 S5D와 **같은 바이너리 형식**으로 만든다. 원천만 바꾼다.

그래프 다섯 섹션은 이미 `SchemaKind=TbStar` 또는 `WebDdw`이면 DDW SQL로 읽어 `SaveToCache`에 넣을 수 있다. 그 상태로 운영 폴더의 `x1`을 덮어쓰면 안 된다. sidecar, 일배치 설비 목록, Hookup, TbStar의 Tag는 아직 S5D이거나 비어 있다. 캐시 폴더 키를 분리한 뒤, 그래프 → sidecar → 설비 목록 순으로 닫는다.

JSearchMK가 보여주는 원격 조회는 **대화형 일부 추출**이다. PocTrace 캐시는 **DB 전체 스냅샷**이다. 원격 Linked Server 쿼리를 캐시 작성기로 그대로 쓰지 않는다.

## 3. 지금 캐시가 기대하는 것

공유 경로 기본값은 `X:\jHOOK\PocTraceData`이다. Admin만 쓴다. 읽는 쪽은 `AllowDatabaseFallback=false`라서 캐시가 없으면 DB로 넘어가지 않는다. DB fallback이 켜져도 그 결과는 메모리뿐이고 `SaveToCache`를 호출하지 않는다.

`TargetDatabases`의 한 이름(예: `x1`)이 폴더 키다.

```
{SharedCachePath}/
  GraphCache_{db}/          current.txt → g{utc}_{id}/
    manifest.bin            magic PTCH, version 10
    nodes.bin  pocs.bin  pipes.bin  takeoffs.bin  dynprops.bin
  BimGroupCache_{db}/       magic PTBG, version 2
  TemplateEqpCache_{db}/    magic PTTE, version 1
```

| 섹션 | 캐시에 들어가는 의미 | 버전 메모 |
|---|---|---|
| nodes | 설비·노즐·댐퍼·피팅. Tag, 이름, Division, Sub, 건설코드, BimId, PZ | v6~v8 필드. BimGroupName은 노드 SQL에 넣지 않는다. |
| pocs | 모델에 달린 연결점. Outlet은 POC당 하나 | |
| pipes | 일반 배관. 길이, Utility, 회사, 끝점 목록 | v10은 `PipeEndpoint` 개수+목록. v9 이하는 Outlet1~3만 끝점 0..2로 읽는다. |
| takeoffs | Takeoff/Damper. Tag와 continuation `PipeId` | |
| dynprops | Hookup, PocNo, ConnectorUse | v9부터 ConnectorUse. 빈 ConnectorUse는 조회 시 Y. |
| BimGroup | 그룹 id, 이름, 부모. capa 제외 | 그래프 행수에 합산하지 않는다. |
| TemplateEqp | Sub1이 `3-%`인 템플릿 설비 | 철거 설비정보 전용. Trace 노드에 넣지 않는다. |

신선도는 그대로다. 로드 시 24시간이면 경고, Viewer는 48시간이 넘으면 시작을 막는다. Admin은 재생성이 목적이라 그 게이트로 막지 않는다.

일배치의 설비 목록은 그래프와 따로다. `SqlQueries.GetSelectAllEquipments`가 S5D `ObjectInstanceLog`에서 Tag가 있는 `1-%`/`6-%` 설비를 고른 뒤, 캐시의 `FindStartPocs(Tag)`로 추적을 시작한다. 그래프만 DDW로 바꿔도 이 목록이 S5D이면 일배치 추적은 DDW 설비를 걷지 않는다.

## 4. 코드가 이미 닫은 것

`App.config`의 `SchemaKind`는 `S5d` | `TbStar` | `WebDdw`이다. 빈 값과 기본은 `S5d`이다. 쿼리가 실패해도 다른 값으로 다시 실행하지 않는다.

`DdwGraphQueryProvider`는 한 연결, 한 카탈로그에서 아래를 읽어 `DdwGraphNormalizer.Build`로 DTO를 만든다.

| 조회 | 조건 |
|---|---|
| 모델 | `TB_MODELINSTANCES` ⋈ 템플릿 ⋈ `TB_DIVISION`. `OPERATIONTYPE < 2` |
| 모델 POC | `TB_POCINSTANCES.OWNER_TYPE = 768` |
| 배관 POC | `OWNER_TYPE = 256`. PocData로 내보내지 않고 배관 끝점 입력만 된다. |
| 배관 | `TB_PIPEINSTANCES`. `PIPE_TYPE = 17301768`은 Takeoff만. 일반 `PipeData`에서 뺀다. |
| 속성 | `TB_ATTRIBUTES`에서 `Element ID_Number`, `__MODULE_INFO`만 |

정규화 규칙(계약 1.2.0과 같다):

- live는 `OPERATIONTYPE < 2`. `TB_VERSIONS` 조인으로 live를 가르지 않는다. 역방향 `TOP 1`은 없다.
- `CONNECTED_POC_ID`가 비거나 zero GUID면 미연결. live POC에 직접 붙고, 상대가 768이면 POC id, 256이면 소유 인스턴스 id. 조인 실패는 dangling으로 세고 간선만 생략한다.
- 배관 끝점은 `CONNECTION_ORDER`, 같으면 POC id 순으로 전부 남긴다. 빈 칸을 앞으로 당기지 않는다. 4개 이상 약 18,316, 유형별 최대 99와 115가 관측돼 있다.
- 길이·좌표 SQL에 ×1000을 넣지 않는다. `LENGTH1 <= 0`이면 0, 아니면 소수 1자리. S5D만 미터 저장분을 쿼리에서 ×1000 한다.
- 수직축은 `ROUND(POSZ, 0)`. S5D의 `PY * 1000`과 섞지 않는다.
- `Element ID_Number`는 `BimId`다. Tag로 넣지 않는다. WebDdw Tag는 `bim_yn=0`이면 `tag`, `bim_yn=1`이면 비어 있지 않은 `s5_eqcode`, 없으면 `tag`.
- 회사명은 `TB_NEW_USERGROUPS.USER_GROUP_NAME`. `COMPANY_NAME` 컬럼을 쓰지 않는다.
- Size·Utility는 POC 인스턴스 값, 비면 템플릿. 연결된 배관 Utility로 POC를 채우지 않는다.
- Utility 마스터는 LEFT JOIN이다. 마스터가 없어도 행을 버리지 않는다. JSearchMK 노즐 조회와 같은 선택이다.
- 미분류 어휘는 노드로 넣지 않고 진단 카운트만 올린다. Equipment로 세지 않는다.
- Hookup 목록은 빈 리스트로 저장한다. 주석과 로그가 D-03 전이라고 적는다.

Admin의 전체 추적과 캐시만 갱신(`RunCacheOnlyAsync`) 모두 이 provider로 그래프를 읽은 뒤 `SaveToCache`로 v10 generation을 쓰고 `current.txt`만 바꾼다. 읽는 쪽은 이전 generation을 유지한다.

`PocTrace.DdwAcceptance`는 합성 fixture로 끝점 왕복과 프로파일 SQL을 본다. 공유 캐시 폴더에서 실행하지 않는다. 그 폴더가 있으면 시험 중 지운다.

## 5. 캐시가 아직 DDW로 닫히지 않는 곳

| 구멍 | 지금 동작 | 캐시에 미치는 일 |
|---|---|---|
| 폴더 키 | `GraphCache_{TargetDatabases 이름}` 하나 | `SchemaKind`만 DDW로 두고 이름 `x1`로 저장하면 S5D 운영 캐시를 덮는다. |
| sidecar | `GetSelectBimGroups`, `GetSelectTemplateEquipments`가 `ConfigurationGroupLog` / `ObjectInstanceLog` | DDW 카탈로그에는 그 로그 테이블이 없다. 그룹·3-% 캐시는 실패하거나 비고, 그래프 저장은 이미 끝난 뒤라 그래프만 남을 수 있다. 실패는 잡이 삼키고 다음으로 간다. |
| 일배치 설비 | `GetSelectAllEquipments`도 S5D 로그. Sub1 `1-%`/`6-%`, Tag 필수, 건설코드 Delete 제외 | DDW 그래프를 만들어도 추적 대상 목록은 S5D다. |
| TbStar Tag | 모델 SQL의 Tag가 `NULL`. `Element ID_Number`는 BimId로만 간다 | 노드가 캐시에 있어도 Tag가 비면 `FindStartPocs`와 일배치가 설비를 못 찾는다. WebDdw가 Tag를 채우는 프로파일이다. |
| Hookup | dynprops가 빈 채 v10으로 저장된다 | Hookup 시트·ConnectorUse 검수는 이 캐시에서 나오지 않는다. 빈 ConnectorUse는 조회 시 Y라서, 빈 목록은 "전부 Y"로 보일 수 있다. |
| 삭제 배관 도구 | `SchemaKind != S5d`이면 시작을 거부한다 | DDW 캐시와 무관한 S5D 로그 스냅샷이다. DDW 캐시 범위에 넣지 않는다. |
| 증분 | `VERSIONS`는 워터마크 후보이나 미구현 | 캐시는 DB 전체 SQL 후 섹션 전체 저장이 기본이다. |

TbStar에서 Tag가 비는 것은 버그로 메우지 않는다. 계약이 ElementId를 Tag로 쓰지 말라고 한다. Tag 원천이 확정되기 전에는 WebDdw로 캐시를 만들거나, TbStar 캐시는 기하·연결 점검용으로만 둔다.

## 6. JSearchMK 원격 DDW 조회에서 가져올 것

X2-M은 FRM 서버에 접속한 뒤 Linked Server `remote_monami.monami.dbo.tb_*`를 4-part 이름으로 읽는다. 테이블 이름은 PocTrace의 `TB_*`와 같고, 대소문자 구분이 없는 인스턴스에서는 같은 객체다. 컬럼은 소문자로 적혀 있다(`instance_id`, `posz`, `version_sequence`).

그 코드가 이미 확인한 운영 사실:

- 접속 카탈로그는 데이터가 있는 DB가 아니라 `master`일 수 있다. 데이터 위치는 4-part 이름이다.
- 원격 테이블을 로컬 임시 테이블과 조인하면 원격 전체가 넘어와 수 분이 걸린다. 그룹명, `attribute_nm`, 삭제 조건은 원격 `WHERE`의 리터럴로 둔다.
- 삭제 행은 그 사이트에서 `version_sequence < 1000000000`으로 거른다. 주석은 이것이 `operationtype < 2`와 같은 뜻이라고 한다.
- `instance_id`를 `nvarchar`로 바꾸면 글자가 깨진다. 다시 필터할 때는 `0x` 16진 리터럴을 쓰고, 한 번에 200개 단위다.
- 속성 필터(`Element ID_Number`, `ConnectionTag`, `BIM_DESIGN_INFO`, `S5_LATERAL NUMBER`, `ConvertDate`)는 필요한 이름만 건다.
- 노즐 번호 끝 3자리 `000`은 SQL에서 자르지 않는다. 원격 `WHERE`에 함수 필터를 더하면 느려져서, 조회 후 프로그램이 뺀다.
- 이력·작업자·ConvertDate는 전체 스냅샷이 아니다. 차이 난 객체만 원격 `IN`으로 읽는다. 한 조회가 실패해도 나머지는 채운다.
- 그룹명 `capa` 제외, Utility LEFT JOIN, 결과에 안 쓰는 마스터(`tb_materials`)는 조인하지 않음. 조인하면 삭제된 마스터가 본 행을 떨어뜨린다.

캐시 작성에 적용하는 규칙:

1. 전체 스냅샷은 **DDW 카탈로그에 직접** 붙는다. `DdwSql`은 `{db}.dbo.TB_*`이다. `master`에 붙고 4-part로 읽는 형태가 아니다.
2. 그 카탈로그에 직접 붙을 수 있으면 그 경로만 쓴다. Linked Server는 대화형 점검과, 직접 접속이 없을 때의 예외 추출이다.
3. 예외 추출을 만들더라도 원격 `WHERE`에 필터를 두고, 로컬 임시 테이블과 조인하지 않는다. 노즐 쿼리를 그래프 쿼리로 재사용하지 않는다. 노즐 쿼리는 BIM Group 일부와 노즐 템플릿만 가져온다.
4. `version_sequence < 1000000000`과 `OPERATIONTYPE < 2`를 한 문장에 섞지 않는다. PocTrace 캐시의 live 문은 `OPERATIONTYPE < 2`이다. 어떤 사이트가 전자만 믿을 수 있으면, 그 카탈로그에서 두 조건의 행수가 같은지 확인한 뒤에만 그 사이트의 삭제 문으로 바꾼다. `TB_VERSIONS`를 live 판정에 조인하지 않는다.
5. `TB_ATTRIBUTES`는 약 1.8억 행이다. 캐시 SQL은 지금처럼 이름 목록만 읽는다. 이름을 늘릴 때도 한 번의 `IN`이다. OWNER별 상관 서브쿼리를 만들지 않는다.
6. GUID를 문자열 키로 왕복하지 않는다. 캐시 안의 키는 `uniqueidentifier`에서 온 `Guid`이다.

## 7. S5D 섹션을 DDW 어디에 연결하나

그래프 키는 `INSTANCE_ID`이다. Tag는 중복될 수 있어 조인 키로 쓰지 않는다.

| 캐시 | S5D 원천 | DDW 원천 | 캐시에 쓰기 전 절단 |
|---|---|---|---|
| nodes | `ObjectInstanceLog` ⋈ `ObjectLog` ⋈ Subdivision | 모델 ⋈ 템플릿 ⋈ Division, 또는 WebDdw 이름 컬럼 | Damper → Nozzle → Equipment → Fitting. 없으면 노드에서 제외. Equipment는 Division/Sub 제외목록, 건설코드 Delete, `PZ < 100000`, Composite, 부모 null-guid를 같이 본다. `MODEL_TYPE` 50594049/50594048만으로 설비를 만들지 않는다. 그 값에는 Grating이 섞여 있다. 모듈 자식은 부모를 거친다. |
| pocs | `ConnectionPointInstanceLog` | `OWNER_TYPE=768` POC ⋈ 템플릿 ⋈ 구경 ⋈ Utility | 256 행은 여기 넣지 않는다. |
| pipes | `PipeLog` Outlet1~3, Origin=0 | `PIPE_TYPE <> 17301768`, 끝점은 256 POC | S5D Origin=0에 해당하는 필터를 DDW에 임의로 추가하지 않는다. |
| takeoffs | `TakeoffLog` | `PIPE_TYPE=17301768`만. `TAKEOFF_YN`만으로 분류하지 않는다 | `PipeId`는 끝점 중 가장 이른 일반 배관 id(현 `ContinuationPipeId`). 대표 1건이 Core continuation과 다르면 가설을 고치고 계약을 고친 뒤에 캐시 의미를 고정한다. |
| dynprops | `W5D_DYNAMICPROPERTIESINSTANCELOG` | DDW 테이블에 Hookup 열이 없다. 후보는 `W5D_CMM.HOOKUPNUMBER` 또는 속성 피벗 | D-03에서 owner id가 배관 id, 노드 id, POC `PointID` 중 어디인지 정해지기 전에는 빈 목록. POC `INSTANCE_ID`를 `PointID`로 단정하지 않는다. |
| BimGroup | `ConfigurationGroupLog` | `TB_INSTANCEGROUPS` / `TB_INSTANCEGROUPMEMBERS`, live, capa 제외, 부모 체인 | 노드 SELECT에 중첩하지 않는다. JSearchMK도 그룹 필터를 원격에 두되, 캐시는 그룹 일부가 아니라 트리 전체다. |
| TemplateEqp | Sub1 `3-%` Equipment | 같은 Equipment 판정과 Sub1 `3-%` | 그래프 노드 수에 넣지 않는다. |
| 일배치 설비 | Sub1 `1-%` 또는 `6-%`, Tag, Delete 제외 | 같은 술어를 DDW 노드 결과 또는 별도 SQL에 적용 | 그래프 노드와 다른 목록이다. Tag가 있는 행만. |

WebDdw에서만 쓰는 추가 컬럼: 모델 `tag`, `s5_eqcode`, `construction_code`, `elementId_number`. 템플릿 `Subdivision_1_name`, `Subdivision_2_name`. POC `tag`, `connectionTag`. 파이프 `sb_future`, `s5_system`은 현 `PipeData`에 없다. TbStar SQL에 이 이름을 넣지 않는다. WebDdw는 로드 전에 위 컬럼(파이프 두 항목 제외, 코드가 검사하는 8개)이 있는지 보고, 없으면 그 SchemaKind 그대로 실패한다.

건설코드는 WebDdw 컬럼이 있을 때만 채운다. TbStar는 null이다. S5D의 Delete 제외는 건설코드 문자에 의존하므로, TbStar에서는 그 제외가 동작하지 않는다.

`InstanceNumericId`는 `DotNetGuidHash.FromGuid`이다. S5D UDF `GetDotNetHashCode`와 대표 쌍이 맞기 전에는 이 값을 캐시 조인 키로 바꾸지 않는다. 그래프 키는 Guid다.

## 8. 생성 절차

### 8.1 프로파일과 폴더를 먼저 고정한다

연결한 카탈로그의 실제 컬럼으로 하나만 고른다.

- TbStar: `TB_MODELINSTANCES`에 `tag`, `s5_eqcode`, `construction_code`, `elementId_number`가 없다. Division은 `TB_DIVISION` 조인.
- WebDdw: 그 컬럼과 템플릿 `Subdivision_1_name`, POC `tag`/`connectionTag`가 있다.

운영 캐시의 `TargetDatabases`에 새 키를 둔다. 예: `ddw_x2m`. `x1`, `x2`, `x2_2`에 DDW 결과를 쓰지 않는다. 같은 `SharedCachePath` 아래 다른 폴더다. 사용자별·exe별 복사본은 만들지 않는다.

Admin은 `D:\PocAdmin`에서만 실행한다. 비밀번호는 그 폴더의 `config\db.secret`만 쓴다. `SchemaKind`는 exe.config 평문이다. 시험 도구 `PocTrace.DdwAcceptance`는 이 공유 경로를 쓰지 않는다.

직접 접속이 되는지, Linked Server로만 보이는지를 이 단계에서 적는다. 직접 접속이면 8.2로 간다. Linked Server뿐이면 8.5를 끝내기 전에는 `SaveToCache`를 호출하지 않는다.

### 8.2 그래프 스냅샷

`SchemaKind`를 고른 값으로 두고, 캐시만 갱신 경로로 그 DB 키 하나만 돌린다. 기대 파일은 `GraphCache_ddw_x2m/current.txt`와 v10 다섯 섹션이다.

쿼리 형태는 지금 `DdwSql`을 유지한다.

- 카탈로그 이름은 허용 목록을 통과한 뒤 괄호로 인용한다.
- live는 `OPERATIONTYPE < 2`.
- 속성은 두 이름으로 제한한다. `__MODULE_INFO`는 배치로 읽어 `ModuleName=`만 파싱한다. 행이 불어나면 그 속성만 분리한다.
- 명령 제한 시간은 전체 추출이라 0(무제한)이다. 대화형 프로브(JSearchMK 15초)와 같지 않다.
- 로드 끝 로그의 모델 POC, 배관 POC, 연결, 미연결, dangling, 4끝점 이상, 최대 끝점, 순번 중복, 미분류 어휘를 보관한다. 원문 Tag와 GUID는 로그에 넣지 않는다.

이 단계의 dynprops는 빈 목록이 정상이다. 빈 목록을 Hookup 완료로 보지 않는다.

### 8.3 sidecar

그래프 SQL과 다른 문장으로, 같은 카탈로그에서 읽는다.

- BimGroup: live 그룹과 멤버. 이름 공백 제거, capa 제외, 부모 id. S5D처럼 같은 id는 최신 1건이면 그 규칙을 DDW의 live 정의에 맞게 한 번만 적용한다. 노드 OUTER APPLY는 넣지 않는다.
- TemplateEqp: Equipment 판정을 통과하고 Sub1이 `3-%`인 행만.
- 둘 다 실패해도 이미 쓴 그래프 generation을 롤백하지 않는다. 지금처럼 경고 후 진행하되, DDW 키의 일배치 추적은 sidecar 실패를 성공으로 집계하지 않는다.

S5D 키의 sidecar SQL은 유지한다. DDW 문장을 S5D 카탈로그에 실행하지 않는다.

### 8.4 설비 목록

일배치가 추적할 Tag 목록을 DDW에서 만든다. 그래프 노드를 다시 거르거나, 같은 술어의 SQL을 따로 쓴다.

- Sub1 `1-%` 또는 `6-%`
- Tag가 비어 있지 않음
- 건설코드에 Delete가 있으면 제외. TbStar는 건설코드가 없으므로 이 제외를 다른 열로 추측하지 않는다.
- 목록의 Tag가 방금 쓴 nodes의 Tag와 만나야 `FindStartPocs`가 시작점을 찾는다.

WebDdw가 이 조건을 만족하기 전에는 일배치 추적 산출(Excel, HTML)을 운영 결과로 배포하지 않는다. 캐시 파일만 연구 키 아래에 둔다.

### 8.5 Linked Server뿐인 경우

직접 `monami`(또는 해당 DDW 카탈로그)에 붙는 권한을 먼저 요청한다. 전체 추출은 그 접속이 맞다.

그 접속이 없을 때 허용하는 추출:

- 4-part 이름, 원격 측 `WHERE`, 필요한 속성 이름만, Utility는 LEFT JOIN.
- 결과를 같은 DTO로 정규화한 뒤, 연구용 폴더 키에만 `SaveToCache`.
- 그룹 `LIKE`로 자른 노즐 집합은 전체 그래프가 아니다. 그 결과로 `GraphCache_*`를 만들지 않는다.
- 이력 200건 배치는 캐시 입력이 아니다. dynprops나 설계일을 나중에 보강할 때의 패턴으로만 남긴다.

### 8.6 읽는 쪽에 넘기는 순서

1. v9와 v10을 모두 읽는 빌드를 Viewer, Web, 철거, 노즐에 먼저 둔다. 지금 `main`은 v10을 읽고, v9는 끝점 3개로 복원한다.
2. S5D 운영 키는 그 빌드의 Admin으로 다시 저장해 둔다. DDW 실험이 S5D 폴더를 건드리지 않게 한다.
3. DDW 키만 `SchemaKind`에 맞게 저장한다.
4. 구 reader에게 v10 파일을 먼저 보내지 않는다.
5. 같은 의미의 설비 1건, 본선 또는 Takeoff 1건, 끝점 4개 이상 1건을 S5D 결과와 비교한다. 터미널 종류, 분기 수, 길이 단위를 본다. 총 행수 비율만으로 합격시키지 않는다.

증분 저장은 하지 않는다. `VERSIONS`를 워터마크로 남기는 것은 전체 스냅샷이 안정된 다음이다. 섹션 하나만 갱신하는 `SaveSection`은 그 섹션이 이미 v10일 때만 가능하다.

## 9. 단계와 완료

| 단계 | 하는 일 | 완료 |
|---|---|---|
| C0 | 이 문서. 코드 변경 없음 | 문서가 `docs/`에 있다. |
| C1 | 연결 카탈로그가 TbStar인지 WebDdw인지, 직접 접속인지 Linked Server뿐인지 기록 | SchemaKind 하나와 폴더 키 하나. 나머지 프로파일 SQL은 실행하지 않는다. |
| C2 | 연구 키에 그래프 v10만 저장 | 진단 카운터가 남고, S5D 키의 파일 시각은 그대로다. v9를 읽으면 끝점이 3개를 넘지 않는다. |
| C3 | DDW sidecar SQL을 Admin의 그 키에만 연결 | 그룹·3-%가 그래프 노드 수에 합산되지 않는다. S5D 키 sidecar는 행 의미가 같다. |
| C4 | DDW 설비 목록 | WebDdw Tag로 시작점을 찾고, 대표 1건의 분기 수·길이가 비교 기준과 같다. TbStar는 Tag 원천이 생기기 전에 이 단계를 통과로 적지 않는다. |
| C5 | D-03 이후에만 dynprops | 승인된 익명 1건이 배관 스텝 또는 POC 검증의 실제 키와 맞다. 맞지 않는 도메인은 보고서에 미지원으로 남는다. |
| C6 | 읽는 쪽 배포 후 DDW 키를 공유 경로에 발행 | `manualVdi` 3경로(설비, Takeoff, 4끝점 이상). dangling은 그 코호트에 있는지만 확인한다. |

계약의 단계 0~8 가운데 끝점 컬렉션, 순회, 캐시 v10, provider 경계, 그래프 매핑은 코드에 들어와 있다. 캐시 전략에서 남은 구현은 C3~C6이다. C1은 설정과 확인이지 SQL 추가가 아니다.

합성 테스트 id는 `PIPE_1`처럼 가명만 쓴다. 수용 이름은 핸드오프와 같다. `S5D-01`, `S5D-02`, `DDW-01`~`DDW-07`, `GRAPH-01`, `CACHE-01`, `CACHE-02`.

## 10. 하지 않는 것

- S5D `SqlQueries`의 업무 규칙을 DDW에 맞추려고 고치지 않는다.
- 쿼리 실패로 SchemaKind를 바꾸지 않는다.
- `Element ID_Number`를 Tag에 넣지 않는다.
- 연결 배관 Utility로 POC를 채우지 않는다.
- `TAKEOFF_YN`만으로 Takeoff를 만들지 않는다.
- 역방향 `TOP 1`, `TB_VERSIONS` live 조인, 끝점 null 압축, DDW SQL의 ×1000을 넣지 않는다.
- 노드 쿼리에 BIM Group을 OUTER APPLY하지 않는다.
- JSearchMK 노즐 SQL, 동료 Java SQL을 캐시 문장으로 복사하지 않는다.
- 운영 `x1`/`x2` 캐시를 DDW 결과로 덮지 않는다.
- Hookup이 비어 있는 v10을 Hookup 검수 완료로 배포하지 않는다.
- 삭제 배관 길이 도구의 별도 스냅샷을 이 캐시에 섞지 않는다.

## 11. 아직 열린 결정

캐시 착수 전에 C1에서 닫을 것:

- D-01. 실행 DB가 TbStar인지 WebDdw인지.
- 직접 카탈로그 접속이 있는지, `remote_monami`뿐인지.
- 그 사이트에서 `version_sequence < 1000000000`과 `OPERATIONTYPE < 2`가 같은 행집합인지. 확인 전에는 후자만 쓴다.

캐시 이후에도 코드에서 추측하지 않을 것:

- D-02. 미분류 Sub 어휘의 업무 이름. 지금은 노드에서 빠진다.
- D-03. Hookup owner 도메인. C5 전.
- D-05. Utility가 인스턴스와 템플릿 둘 다 비는 경우. 배관 값으로 채우지 않는다.
- D-06. Takeoff continuation 방향. C2 진단과 대표 1건으로 확인.
- D-09(통합설계). 모듈 자식 POC의 OwnerId를 부모로 올릴지. 기본은 `OWNER_INSTANCE_ID` 원값.

D-07(캐시 writer가 v9 3슬롯만 쓴다)은 현재 코드 기준으로 닫혀 있다. writer는 v10 목록을 쓴다. 남은 일은 writer 형식 변경이 아니라, DDW 키로 그 writer를 호출하고 sidecar를 붙이는 일이다.

## 12. SVDI 조회 루프

이제 원본은 Linked Server `remote_monami.monami.dbo`로 읽는다. SSMS는 이 링크가 등록된 FRM 서버에 붙는다. monami 창에 직접 붙는 접속이 아니다. 이 경로로 테이블 명세와 집계, 잘린 샘플을 받고, 그 회신으로 캐시 SQL을 고정한다. 아래 문장은 캐시 작성기가 아니다. 큰 테이블을 통째로 내리지 않는다.

쿼리 파일은 `docs/ddw-svdi-queries/`에 있고, 같은 내용의 zip은 `docs/DDW-SVDI-Queries.zip`이다. 사무실에는 zip만 가져간다. 본문은 13절과 같다.

한 번에 파일 하나. 비밀번호, 로그인, 전화, 메일, BLOB는 읽지 않는다. GUID와 설비 이름은 앞자리나 길이만 나온다. 결과에 그런 전문이 보이면 지우고 보낸다. 링크 이름이 `remote_monami`가 아니면 01의 `linked_server`만 보고, 그 이름으로 4-part의 첫 조각만 바꾼다. 테이블 이름은 에러가 나도 추측해서 바꾸지 않는다. 에러는 앞 300자만 보낸다. SLOW 파일은 10분이 넘으면 취소하고 `TIMEOUT`이라고 보낸다.

회신 예:

```
Q=07
분=1
결과=
(그리드 그대로)
```

| 순서 | 파일 | 속도 | 이 결과로 닫는 것 |
|---|---|---|---|
| 1 | `01-probe.sql` | 빠름 | 링크 이름, 카탈로그, 핵심 테이블이 한 행이라도 읽히는지 |
| 2 | `02-tables-and-rows.sql` | 빠름 | 모든 테이블 이름과 대략 행수 |
| 3 | `03-columns-core.sql` | 빠름 | 캐시 대상 테이블의 열, 타입, NULL |
| 4 | `04-columns-other.sql` | 빠름 | 나머지 테이블의 열 명세 |
| 5 | `05-name-hits.sql` | 빠름 | `tag`, `s5_eqcode`, `construction_code`, `elementId_number` 등이 있는지. D-01 |
| 6 | `06-objects-like-version-hook.sql` | 빠름 | `tb_versions`, Hookup, CMM 객체가 이 DB에 있는지 |
| 7 | `07-division-vocab.sql` | 빠름 | `1-%`, `3-%`, `6-%`, TAKE-OFF, Damper, BIM 문자열이 Division에 있는지 |
| 8 | `08-small-masters.sql` | 빠름 | Utility, 구경, POC 유형, 회사명 열이 채워졌는지. 사람 이름은 안 읽음 |
| 9 | `09-group-shape.sql` | 보통 | 그룹 부모 열, capa 건수. sidecar |
| 10 | `10-shape-samples.sql` | 빠름 | 모델, 템플릿, POC, 배관, POC 템플릿의 행 모양. 분포 아님 |
| 11 | `11-template-sub-top.sql` | 보통 | Sub1 상위 40개. 노드 분류 어휘 |
| 12 | `SLOW-01-live-gate.sql` | 느림 | `OPERATIONTYPE < 2`와 `version_sequence < 1000000000`이 같은 행인지 |
| 13 | `SLOW-02-pipe-type.sql` | 느림 | `PIPE_TYPE`별 건수. 17301768 |
| 14 | `SLOW-03-poc-ends.sql` | 느림 | owner 768/256, 끝점 1·2·3·4개 이상, 최댓값 |
| 15 | `SLOW-04-connect-order.sql` | 느림 | 미연결, zero GUID, 순번 중복, dangling |
| 16 | `SLOW-05-units.sql` | 느림 | 길이, 구경, POSZ/POSY의 최소·평균·최대. ×1000 여부 |
| 17 | `SLOW-06-model-type.sql` | 느림 | `MODEL_TYPE`별 건수와 Composite, 부모 |
| 18 | `SLOW-08-takeoff-flags.sql` | 느림 | `TAKEOFF_YN` 건수와 17301768 건수를 따로 |
| 19 | `SLOW-07-attributes.sql` | 가장 느림 | 필요한 속성 이름만. Element ID, 모듈, Hookup, ConnectorUse |

첫 방문은 01부터 10까지다. 11과 SLOW는 그 다음이다. SLOW-07은 항상 마지막이다.

빈 칸은 `RESULT-TEMPLATE.txt`에 맞춰 채운다. 그리드가 길면(02, 03, 04) 사진이어도 된다. 집계 쿼리는 숫자 행이라 타이핑으로 충분하다.

## 13. 쿼리 본문

아래 본문은 `docs/ddw-svdi-queries` 와 `docs/DDW-SVDI-Queries.zip` 과 같다. SSMS에는 zip 안의 파일을 연다.

### 00-README.txt

```text
DDW remote_monami 조회 팩
=========================

SSMS는 Linked Server가 등록된 FRM 서버에 접속한다.
데이터는 4-part 이름 remote_monami.monami.dbo.* 로 읽는다.
monami에 직접 붙은 창이 아니면 이 팩을 그대로 쓴다.
링크 이름이 다르면 01 결과의 linked_server 이름을 확인하고, 쿼리의 remote_monami 만 그 이름으로 바꾼다. 테이블 이름은 추측해서 바꾸지 않는다.

한 번에 파일 하나. 결과에서 안내한 열만 복사한다.
비밀번호, 로그인, 전화, 메일, BLOB 는 읽지 않는다. 그런 값이 보이면 지우고 보낸다.
식별자는 앞 8자 또는 길이만 나온다. 설비 Tag 전문을 추가로 복사하지 않는다.

느린 파일(SLOW-*)은 원격 큰 테이블을 집계한다. 로컬 임시 테이블과 조인하지 않는다.
10분이 넘으면 취소하고 TIMEOUT 이라고만 보낸다. 속성 집계(SLOW-07)는 가장 마지막이다.

회신 형식
----------
Q=파일번호
분=걸린 시간
결과=
(그리드 그대로)

에러면 결과 대신 메시지 앞 300자.

첫 방문: 01, 02, 03, 04, 05, 06, 07, 08, 09, 10
다음: 11, SLOW-01, SLOW-02, SLOW-03, SLOW-04, SLOW-05, SLOW-06, SLOW-08
마지막: SLOW-07

이 팩은 캐시 작성 SQL이 아니다. 명세와 분포를 확인한 뒤 캐시 쿼리를 고정한다.
```

### RESULT-TEMPLATE.txt

```text
날짜:
SSMS 서버: FRM (링크가 있는 쪽)
링크 이름:

[01]
분=
결과=

[02]
분=
결과=

[03]
분=
결과=

[04]
분=
결과=

[05]
분=
결과=

[06]
분=
결과=

[07]
분=
결과=

[08]
분=
결과=

[09]
분=
결과=

[10]
분=
결과=

[11]
분=
결과=

[SLOW-01]
분=
결과=

[SLOW-02]
분=
결과=

[SLOW-03]
분=
결과=

[SLOW-04]
분=
결과=

[SLOW-05]
분=
결과=

[SLOW-06]
분=
결과=

[SLOW-08]
분=
결과=

[SLOW-07]
분=
결과=
```

### 01-probe.sql

```sql
-- 01 연결 확인. 빠름.
-- 결과 그리드를 모두 복사한다. 행은 몇 줄이다.
-- 01-B 가 30초를 넘으면 취소하고 01-A 만 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- 01-A 링크 이름만. data_source(주소)는 고르지 않는다.
SELECT name AS linked_server
FROM sys.servers
WHERE is_linked = 1
ORDER BY name;

-- 01-B 카탈로그 이름
SELECT TOP 1 TABLE_CATALOG AS remote_catalog
FROM remote_monami.monami.INFORMATION_SCHEMA.TABLES;

-- 01-C 핵심 테이블이 한 행이라도 읽히는지. 분포가 아니다.
SELECT TOP 1
    'tb_instancegroups' AS src,
    CASE WHEN instance_group_id IS NULL THEN 0 ELSE 1 END AS has_id,
    operationtype
FROM remote_monami.monami.dbo.tb_instancegroups WITH (NOLOCK);

SELECT TOP 1
    'tb_modelinstances' AS src,
    model_type,
    composite_yn,
    operationtype,
    version_sequence,
    ROUND(posz, 1) AS posz
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK);

SELECT TOP 1
    'tb_pocinstances' AS src,
    owner_type,
    connection_order,
    operationtype,
    CASE
        WHEN connected_poc_id IS NULL THEN 'null'
        WHEN connected_poc_id = '00000000-0000-0000-0000-000000000000' THEN 'zero'
        ELSE 'set'
    END AS connected
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK);

SELECT TOP 1
    'tb_pipeinstances' AS src,
    pipe_type,
    operationtype,
    CASE WHEN length1 IS NULL THEN NULL ELSE ROUND(length1, 3) END AS length1
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

SELECT TOP 1
    'tb_attributes' AS src,
    LEFT(attribute_nm, 40) AS attribute_nm,
    owner_type,
    operationtype
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK);
```

### 02-tables-and-rows.sql

```sql
-- 02 모든 테이블 이름과 대략 행수. 빠름.
-- sys.partitions 의 행수라 정확한 COUNT 가 아니다.
-- 조인은 원격끼리만 한다. 결과 열은 table_name, row_count. 표를 통째로 복사하거나 사진을 찍는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    t.name AS table_name,
    SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS row_count
FROM remote_monami.monami.sys.tables AS t
JOIN remote_monami.monami.sys.partitions AS p
    ON p.object_id = t.object_id
GROUP BY t.name
ORDER BY t.name;
```

### 03-columns-core.sql

```sql
-- 03 캐시에 쓰는 테이블의 열 명세. 빠름.
-- 원격 메타만 로컬 #cols 로 한 번 가져온다. 큰 테이블과 조인하지 않는다.
-- 결과 열 line 만 복사한다. 테이블당 한 줄이다.
-- 사용자 테이블에 LOGIN_PW 같은 열 이름이 보여도 값은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#cols') IS NOT NULL DROP TABLE #cols;

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    ORDINAL_POSITION,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
INTO #cols
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS;

SELECT
    c.TABLE_NAME + ' :: ' + STUFF((
        SELECT
            ' | ' + c2.COLUMN_NAME + ' ' + c2.DATA_TYPE
            + CASE
                WHEN c2.CHARACTER_MAXIMUM_LENGTH IS NULL THEN ''
                WHEN c2.CHARACTER_MAXIMUM_LENGTH < 0 THEN '(max)'
                ELSE '(' + CONVERT(varchar(10), c2.CHARACTER_MAXIMUM_LENGTH) + ')'
              END
            + CASE WHEN c2.IS_NULLABLE = 'NO' THEN ' NN' ELSE '' END
        FROM #cols AS c2
        WHERE c2.TABLE_NAME = c.TABLE_NAME
        ORDER BY c2.ORDINAL_POSITION
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 3, '') AS line
FROM #cols AS c
WHERE LOWER(c.TABLE_NAME) IN (
    'tb_modelinstances',
    'tb_modeltemplates',
    'tb_division',
    'tb_pocinstances',
    'tb_poctemplates',
    'tb_pipesize',
    'tb_pipeinstances',
    'tb_utilities',
    'tb_utility_groups',
    'tb_attributes',
    'tb_instancegroups',
    'tb_instancegroupmembers',
    'tb_new_users',
    'tb_new_usergroups',
    'tb_versions'
)
GROUP BY c.TABLE_NAME
ORDER BY c.TABLE_NAME;
```

### 04-columns-other.sql

```sql
-- 04 나머지 모든 테이블의 열 명세. 빠름.
-- 03 과 같은 방식. line 열만 복사한다. 테이블이 많으면 사진을 찍어도 된다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#cols') IS NOT NULL DROP TABLE #cols;

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    ORDINAL_POSITION,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
INTO #cols
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS;

SELECT
    c.TABLE_NAME + ' :: ' + STUFF((
        SELECT
            ' | ' + c2.COLUMN_NAME + ' ' + c2.DATA_TYPE
            + CASE
                WHEN c2.CHARACTER_MAXIMUM_LENGTH IS NULL THEN ''
                WHEN c2.CHARACTER_MAXIMUM_LENGTH < 0 THEN '(max)'
                ELSE '(' + CONVERT(varchar(10), c2.CHARACTER_MAXIMUM_LENGTH) + ')'
              END
            + CASE WHEN c2.IS_NULLABLE = 'NO' THEN ' NN' ELSE '' END
        FROM #cols AS c2
        WHERE c2.TABLE_NAME = c.TABLE_NAME
        ORDER BY c2.ORDINAL_POSITION
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 3, '') AS line
FROM #cols AS c
WHERE LOWER(c.TABLE_NAME) NOT IN (
    'tb_modelinstances',
    'tb_modeltemplates',
    'tb_division',
    'tb_pocinstances',
    'tb_poctemplates',
    'tb_pipesize',
    'tb_pipeinstances',
    'tb_utilities',
    'tb_utility_groups',
    'tb_attributes',
    'tb_instancegroups',
    'tb_instancegroupmembers',
    'tb_new_users',
    'tb_new_usergroups',
    'tb_versions'
)
GROUP BY c.TABLE_NAME
ORDER BY c.TABLE_NAME;
```

### 05-name-hits.sql

```sql
-- 05 이름에 tag, construction, element, hook, connector, bim, subdivision, s5 가 들어간 열. 빠름.
-- WebDdw 인지 TbStar 인지(D-01)를 이 결과로 닫는다.
-- 열 이름만. 값은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS
WHERE
    LOWER(COLUMN_NAME) LIKE '%tag%'
    OR LOWER(COLUMN_NAME) LIKE '%construction%'
    OR LOWER(COLUMN_NAME) LIKE '%element%'
    OR LOWER(COLUMN_NAME) LIKE '%hook%'
    OR LOWER(COLUMN_NAME) LIKE '%connector%'
    OR LOWER(COLUMN_NAME) LIKE '%bim%'
    OR LOWER(COLUMN_NAME) LIKE '%subdivision%'
    OR LOWER(COLUMN_NAME) LIKE '%s5%'
    OR LOWER(COLUMN_NAME) LIKE '%eqcode%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
```

### 06-objects-like-version-hook.sql

```sql
-- 06 버전, Hookup, CMM, 속성처럼 보이는 테이블과 뷰. 빠름.
-- 이름만. 행 내용은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.name AS object_name,
    o.type_desc
FROM remote_monami.monami.sys.objects AS o
WHERE o.is_ms_shipped = 0
  AND (
        LOWER(o.name) LIKE '%version%'
        OR LOWER(o.name) LIKE '%hook%'
        OR LOWER(o.name) LIKE '%cmm%'
        OR LOWER(o.name) LIKE '%attribute%'
        OR LOWER(o.name) LIKE '%connector%'
        OR LOWER(o.name) LIKE '%dynamic%'
      )
ORDER BY o.type_desc, o.name;
```

### 07-division-vocab.sql

```sql
-- 07 Division 어휘. 빠름. tb_division 은 수백 행이다.
-- 설비 분류(1-%, 3-%, 6-%, TAKE-OFF, Damper, BIM, electrical)가 이 값으로 있는지 본다.
-- 결과 두 그리드를 모두 복사한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    division_type,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_division WITH (NOLOCK)
WHERE operationtype < 2
GROUP BY division_type
ORDER BY division_type;

SELECT
    division_type,
    LEFT(division_value, 80) AS value80,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_division WITH (NOLOCK)
WHERE operationtype < 2
  AND (
        division_value LIKE '1-%'
        OR division_value LIKE '3-%'
        OR division_value LIKE '5-%'
        OR division_value LIKE '6-%'
        OR division_value LIKE 'BIM%'
        OR division_value LIKE '%Nozzle%'
        OR division_value LIKE '%NOZZLE%'
        OR division_value LIKE '%Damper%'
        OR division_value LIKE '%DAMPER%'
        OR division_value LIKE '%TAKE%'
        OR division_value LIKE '%valve%'
        OR division_value LIKE '%VALVE%'
        OR division_value LIKE '%electrical%'
        OR division_value LIKE '%template%'
        OR division_value LIKE '%TEMPLATE%'
      )
GROUP BY division_type, LEFT(division_value, 80)
ORDER BY value80;
```

### 08-small-masters.sql

```sql
-- 08 작은 마스터의 모양. 빠름.
-- 사람 이름, 로그인, 비밀번호, 전화, 메일은 읽지 않는다.
-- 회사명은 값 대신 채워진 행수만 센다.
-- 결과 그리드를 모두 복사한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*) AS utility_rows,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS utility_live
FROM remote_monami.monami.dbo.tb_utilities WITH (NOLOCK);

SELECT TOP 20
    LEFT(utility_nm, 24) AS utility24,
    utility_type,
    operationtype
FROM remote_monami.monami.dbo.tb_utilities WITH (NOLOCK)
WHERE operationtype < 2
ORDER BY utility_nm;

SELECT
    COUNT(*) AS group_rows,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS group_live
FROM remote_monami.monami.dbo.tb_utility_groups WITH (NOLOCK);

SELECT TOP 20
    LEFT(utility_group_nm, 24) AS group24
FROM remote_monami.monami.dbo.tb_utility_groups WITH (NOLOCK)
WHERE operationtype < 2
ORDER BY utility_group_nm;

SELECT TOP 15
    LEFT(pipesize_nm, 24) AS size24,
    pipesize_type,
    ROUND(diameter, 3) AS diameter,
    ROUND(width, 3) AS width,
    ROUND(height, 3) AS height
FROM remote_monami.monami.dbo.tb_pipesize WITH (NOLOCK)
WHERE operationtype < 2
ORDER BY pipesize_nm;

SELECT
    poc_type,
    LEFT(poc_type_value, 40) AS value40
FROM remote_monami.monami.dbo.tb_poctypes WITH (NOLOCK)
WHERE operationtype < 2
ORDER BY poc_type;

SELECT
    COUNT(*) AS user_rows,
    SUM(CASE WHEN company_name IS NULL OR LTRIM(RTRIM(company_name)) = '' THEN 0 ELSE 1 END) AS company_name_filled,
    SUM(CASE WHEN user_group_id IS NULL THEN 0 ELSE 1 END) AS user_group_id_filled
FROM remote_monami.monami.dbo.tb_new_users WITH (NOLOCK);

SELECT
    COUNT(*) AS usergroup_rows,
    SUM(CASE WHEN user_group_name IS NULL OR LTRIM(RTRIM(user_group_name)) = '' THEN 0 ELSE 1 END) AS group_name_filled
FROM remote_monami.monami.dbo.tb_new_usergroups WITH (NOLOCK);
```

### 09-group-shape.sql

```sql
-- 09 BIM Group 모양. 보통. tb_instancegroups 는 약 9만 행.
-- capa 제외와 부모 열이 sidecar 에 필요한지 확인한다.
-- 그룹 이름은 앞 24자만.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*) AS all_rows,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_rows,
    SUM(CASE WHEN operationtype < 2 AND instance_group_nm LIKE '%capa%' THEN 1 ELSE 0 END) AS live_capa,
    SUM(CASE WHEN operationtype < 2 AND (instance_group_parent_id IS NULL
         OR instance_group_parent_id = '00000000-0000-0000-0000-000000000000') THEN 1 ELSE 0 END) AS live_parent_null,
    SUM(CASE WHEN operationtype < 2 AND (instance_group_nm IS NULL OR LTRIM(RTRIM(instance_group_nm)) = '') THEN 1 ELSE 0 END) AS live_name_blank
FROM remote_monami.monami.dbo.tb_instancegroups WITH (NOLOCK);

SELECT TOP 15
    LEFT(instance_group_nm, 24) AS name24,
    instance_group_type,
    CASE
        WHEN instance_group_parent_id IS NULL
          OR instance_group_parent_id = '00000000-0000-0000-0000-000000000000' THEN 1
        ELSE 0
    END AS parent_null
FROM remote_monami.monami.dbo.tb_instancegroups WITH (NOLOCK)
WHERE operationtype < 2
  AND instance_group_nm NOT LIKE '%capa%'
ORDER BY instance_group_nm;
```

### 10-shape-samples.sql

```sql
-- 10 행의 모양 샘플. 빠름. TOP 이라 분포가 아니다.
-- GUID 전문, Tag 전문, 사람 이름은 내지 않는다.
-- 결과 그리드를 모두 복사한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT TOP 8
    'model' AS src,
    model_type,
    composite_yn,
    CASE
        WHEN parent_instance_id IS NULL
          OR parent_instance_id = '00000000-0000-0000-0000-000000000000' THEN 1
        ELSE 0
    END AS parent_null,
    CASE WHEN instance_nm IS NULL THEN 0 ELSE LEN(instance_nm) END AS name_len,
    LEFT(ISNULL(instance_nm, ''), 12) AS name12,
    ROUND(posx, 1) AS posx,
    ROUND(posy, 1) AS posy,
    ROUND(posz, 1) AS posz,
    operationtype,
    version_sequence
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
WHERE operationtype < 2;

SELECT TOP 8
    'template' AS src,
    model_type,
    bim_yn,
    CASE WHEN division_id IS NULL OR LTRIM(RTRIM(division_id)) = '' THEN 0 ELSE 1 END AS has_division,
    CASE WHEN subdivision_1_id IS NULL OR LTRIM(RTRIM(subdivision_1_id)) = '' THEN 0 ELSE 1 END AS has_sub1,
    CASE WHEN subdivision_2_id IS NULL OR LTRIM(RTRIM(subdivision_2_id)) = '' THEN 0 ELSE 1 END AS has_sub2,
    CASE WHEN family_key IS NULL THEN 0 ELSE LEN(family_key) END AS family_len,
    LEFT(ISNULL(display_nm, ''), 16) AS display16,
    LEFT(ISNULL(model_template_nm, ''), 16) AS template16,
    operationtype
FROM remote_monami.monami.dbo.tb_modeltemplates WITH (NOLOCK)
WHERE operationtype < 2;

SELECT TOP 8
    'poc' AS src,
    owner_type,
    connection_order,
    poc_shape,
    ROUND(diameter, 3) AS diameter,
    ROUND(width, 3) AS width,
    CASE WHEN poc_template_id IS NULL THEN 0 ELSE 1 END AS has_template,
    CASE WHEN utility_id IS NULL OR utility_id = '00000000-0000-0000-0000-000000000000' THEN 0 ELSE 1 END AS has_utility,
    CASE
        WHEN connected_poc_id IS NULL THEN 'null'
        WHEN connected_poc_id = '00000000-0000-0000-0000-000000000000' THEN 'zero'
        ELSE 'set'
    END AS connected,
    operationtype,
    version_sequence
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE operationtype < 2;

SELECT TOP 8
    'pipe' AS src,
    pipe_type,
    CASE WHEN length1 IS NULL THEN NULL ELSE ROUND(length1, 3) END AS length1,
    CASE WHEN diameter1 IS NULL THEN NULL ELSE ROUND(diameter1, 3) END AS diameter1,
    CASE WHEN utility_id IS NULL OR utility_id = '00000000-0000-0000-0000-000000000000' THEN 0 ELSE 1 END AS has_utility,
    ROUND(posz, 1) AS posz,
    operationtype,
    version_sequence
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE operationtype < 2;

SELECT TOP 8
    'poctemplate' AS src,
    takeoff_yn,
    equipment_yn,
    CASE WHEN memo IS NULL OR LTRIM(RTRIM(memo)) = '' THEN 0 ELSE LEN(memo) END AS memo_len,
    LEFT(ISNULL(poc_template_nm, ''), 16) AS name16,
    operationtype
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK)
WHERE operationtype < 2;
```

### 11-template-sub-top.sql

```sql
-- 11 템플릿 Sub1 상위 값. 보통. 템플릿 약 120만과 Division 조인.
-- 조인은 둘 다 원격이다. 로컬 임시 테이블과 조인하지 않는다.
-- 노드 분류(Damper, Nozzle, 1-%, 3-%, 6-%)의 실제 문자열을 본다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*) AS live_templates,
    SUM(CASE WHEN bim_yn = 1 THEN 1 ELSE 0 END) AS bim_yn_1,
    SUM(CASE WHEN bim_yn = 0 THEN 1 ELSE 0 END) AS bim_yn_0,
    SUM(CASE WHEN subdivision_1_id IS NULL OR LTRIM(RTRIM(subdivision_1_id)) = '' THEN 1 ELSE 0 END) AS sub1_blank,
    SUM(CASE
            WHEN NULLIF(LTRIM(RTRIM(subdivision_1_id)), '') IS NOT NULL
             AND TRY_CAST(subdivision_1_id AS uniqueidentifier) IS NULL THEN 1
            ELSE 0
        END) AS sub1_not_guid
FROM remote_monami.monami.dbo.tb_modeltemplates WITH (NOLOCK)
WHERE operationtype < 2;

SELECT TOP 40
    LEFT(d.division_value, 40) AS sub1,
    d.division_type,
    COUNT(*) AS templates
FROM remote_monami.monami.dbo.tb_modeltemplates AS t WITH (NOLOCK)
JOIN remote_monami.monami.dbo.tb_division AS d WITH (NOLOCK)
    ON d.division_id = TRY_CAST(t.subdivision_1_id AS uniqueidentifier)
   AND d.operationtype < 2
WHERE t.operationtype < 2
GROUP BY LEFT(d.division_value, 40), d.division_type
ORDER BY COUNT(*) DESC;
```

### SLOW-01-live-gate.sql

```sql
-- SLOW-01 live 조건 비교. 모델, 배관, POC 를 각각 한 번씩 센다.
-- operationtype < 2 와 version_sequence < 1000000000 이 같은 행인지 본다.
-- 한 결과 집합이 끝난 뒤 다음이 시작된다. 각 블록이 10분을 넘으면 그 블록만 취소하고 TIMEOUT 과 테이블 이름을 보낸다.
-- 분포가 맞는지 확인하기 전까지만 쓰는 조회다. 캐시 SQL에 두 조건을 섞지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'tb_modelinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK);

SELECT
    'tb_pipeinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

SELECT
    'tb_pocinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK);
```

### SLOW-02-pipe-type.sql

```sql
-- SLOW-02 배관 PIPE_TYPE 분포. 배관 테이블 전체 집계.
-- 17301768 이 Takeoff 로 몇 건인지, 그 외 유형이 몇 개인지 본다.
-- 결과는 유형 수만큼이라 짧다. 10분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    pipe_type,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_rows,
    SUM(CASE WHEN operationtype >= 2 THEN 1 ELSE 0 END) AS not_live,
    SUM(CASE WHEN operationtype < 2 AND (length1 IS NULL OR length1 <= 0) THEN 1 ELSE 0 END) AS live_length_empty
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
GROUP BY pipe_type
ORDER BY pipe_type;
```

### SLOW-03-poc-ends.sql

```sql
-- SLOW-03 POC owner 와 배관 끝점 개수. POC 테이블 집계.
-- owner_type 768 은 모델 POC, 256 은 배관 끝점이다.
-- 끝점 히스토그램은 1~6개와 90개 이상만 돌려 준다. 4개 이상 합계와 최댓값은 한 줄이다.
-- 10분이 넘으면 TIMEOUT. 끝점 쿼리가 더 길다. 첫 결과(owner_type)만이라도 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    owner_type,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_rows,
    SUM(CASE WHEN operationtype >= 2 THEN 1 ELSE 0 END) AS not_live
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
GROUP BY owner_type
ORDER BY owner_type;

SELECT
    SUM(CASE WHEN n = 1 THEN 1 ELSE 0 END) AS pipes_1,
    SUM(CASE WHEN n = 2 THEN 1 ELSE 0 END) AS pipes_2,
    SUM(CASE WHEN n = 3 THEN 1 ELSE 0 END) AS pipes_3,
    SUM(CASE WHEN n >= 4 THEN 1 ELSE 0 END) AS pipes_4plus,
    MAX(n) AS max_ends
FROM (
    SELECT owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE operationtype < 2
      AND owner_type = 256
    GROUP BY owner_instance_id
) AS s;

SELECT
    n AS endpoint_n,
    COUNT(*) AS pipes
FROM (
    SELECT owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE operationtype < 2
      AND owner_type = 256
    GROUP BY owner_instance_id
) AS s
WHERE n <= 6 OR n >= 90
GROUP BY n
ORDER BY n;
```

### SLOW-04-connect-order.sql

```sql
-- SLOW-04 연결과 순번 품질. POC 집계, 이어서 원격끼리 LEFT JOIN.
-- dangling 조인은 가장 길다. 앞의 두 결과가 나온 뒤 10분이 더 넘으면 dangling 만 취소한다.
-- 역방향 TOP 1 은 만들지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    owner_type,
    SUM(CASE WHEN connected_poc_id IS NULL THEN 1 ELSE 0 END) AS connected_null,
    SUM(CASE WHEN connected_poc_id = '00000000-0000-0000-0000-000000000000' THEN 1 ELSE 0 END) AS connected_zero,
    SUM(CASE WHEN connected_poc_id IS NOT NULL
              AND connected_poc_id <> '00000000-0000-0000-0000-000000000000' THEN 1 ELSE 0 END) AS connected_set
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND owner_type IN (256, 768)
GROUP BY owner_type
ORDER BY owner_type;

SELECT
    COUNT(*) AS owners_with_duplicate_order
FROM (
    SELECT owner_instance_id
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE operationtype < 2
      AND owner_type = 256
    GROUP BY owner_instance_id, connection_order
    HAVING COUNT(*) > 1
) AS d;

SELECT
    COUNT(*) AS dangling_pipe_pocs
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_pocinstances AS t WITH (NOLOCK)
    ON t.instance_id = p.connected_poc_id
   AND t.operationtype < 2
WHERE p.operationtype < 2
  AND p.owner_type = 256
  AND p.connected_poc_id IS NOT NULL
  AND p.connected_poc_id <> '00000000-0000-0000-0000-000000000000'
  AND t.instance_id IS NULL;
```

### SLOW-05-units.sql

```sql
-- SLOW-05 길이, 구경, 좌표의 크기. 단위가 mm 인지 m 인지 본다.
-- 값은 최소, 평균, 최대만. 좌표 원문은 행으로 내지 않는다.
-- 배관과 모델을 각각 집계한다. 10분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'pipe.length1' AS src,
    MIN(length1) AS min_v,
    AVG(length1) AS avg_v,
    MAX(length1) AS max_v
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND length1 IS NOT NULL
  AND length1 > 0;

SELECT
    'pipe.diameter1' AS src,
    MIN(diameter1) AS min_v,
    AVG(diameter1) AS avg_v,
    MAX(diameter1) AS max_v
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND diameter1 IS NOT NULL
  AND diameter1 > 0;

SELECT
    'poc.diameter' AS src,
    MIN(diameter) AS min_v,
    AVG(diameter) AS avg_v,
    MAX(diameter) AS max_v
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND owner_type = 768
  AND diameter IS NOT NULL
  AND diameter > 0;

SELECT
    'model.pos' AS src,
    MIN(posz) AS min_z,
    AVG(posz) AS avg_z,
    MAX(posz) AS max_z,
    MIN(posy) AS min_y,
    MAX(posy) AS max_y,
    SUM(CASE WHEN ROUND(posz, 0) < 100000 THEN 1 ELSE 0 END) AS posz_under_100000,
    SUM(CASE WHEN ROUND(posy, 0) < 100000 THEN 1 ELSE 0 END) AS posy_under_100000,
    COUNT(*) AS live_models
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
WHERE operationtype < 2;
```

### SLOW-06-model-type.sql

```sql
-- SLOW-06 모델 유형과 Composite, 부모. 모델 테이블 집계.
-- MODEL_TYPE 단독으로 설비를 만들지 않는다. 이 숫자는 후보 필터를 확인할 때 쓴다.
-- 50594049, 50594048 이 보이면 그 행의 composite, 부모 비율을 같이 본다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    model_type,
    COUNT(*) AS live_rows,
    SUM(CASE WHEN composite_yn <> 0 THEN 1 ELSE 0 END) AS composite_on,
    SUM(CASE
            WHEN parent_instance_id IS NULL
              OR parent_instance_id = '00000000-0000-0000-0000-000000000000' THEN 1
            ELSE 0
        END) AS parent_null
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
WHERE operationtype < 2
GROUP BY model_type
ORDER BY COUNT(*) DESC;
```

### SLOW-07-attributes.sql

```sql
-- SLOW-07 속성 이름 존재. 가장 느릴 수 있다. 마지막에 한 번만.
-- TB_ATTRIBUTES 는 1억 행이 넘는다. 필요한 이름만 IN 으로 건다.
-- 이름 목록 전체를 집계하지 않는다. 로컬 임시 테이블과 조인하지 않는다.
-- 값은 앞 24자와 길이만. Tag 로 보이는 긴 문자열을 더 복사하지 않는다.
-- 30분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    attribute_nm,
    owner_type,
    COUNT(*) AS live_rows,
    SUM(CASE WHEN attribute_val IS NULL OR LTRIM(RTRIM(attribute_val)) = '' THEN 1 ELSE 0 END) AS blank_val,
    MIN(LEN(attribute_val)) AS min_len,
    MAX(LEN(attribute_val)) AS max_len
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE operationtype < 2
  AND attribute_nm IN (
        N'Element ID_Number',
        N'Element ID',
        N'__MODULE_INFO',
        N'ConnectionTag',
        N'CONNECTIONTAG',
        N'BIM_DESIGN_INFO',
        N'S5_LATERAL NUMBER',
        N'ConvertDate',
        N'HookNumber',
        N'HOOKUPNUMBER',
        N'Hookup Number',
        N'HookUp Number',
        N'ConnectorUse',
        N'CONNECTORUSE',
        N'POCNO',
        N'PocNo',
        N'ConstructionCode',
        N'CONSTRUCTIONCODE',
        N'construction_code'
      )
GROUP BY attribute_nm, owner_type
ORDER BY attribute_nm, owner_type;

SELECT TOP 5
    attribute_nm,
    owner_type,
    LEN(attribute_val) AS val_len,
    LEFT(attribute_val, 24) AS val24
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE operationtype < 2
  AND attribute_nm = N'__MODULE_INFO';
```

### SLOW-08-takeoff-flags.sql

```sql
-- SLOW-08 Takeoff 후보. 템플릿 TAKEOFF_YN 과 배관 유형을 따로 센다.
-- TAKEOFF_YN 만으로 Takeoff 를 분류하지 않는다. 두 수의 차이를 보기 위한 조회다.
-- 템플릿과 배관을 한 조인으로 붙이지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    takeoff_yn,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_templates
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK)
GROUP BY takeoff_yn
ORDER BY takeoff_yn;

SELECT
    equipment_yn,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_templates
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK)
GROUP BY equipment_yn
ORDER BY equipment_yn;

SELECT
    SUM(CASE WHEN operationtype < 2 AND pipe_type = 17301768 THEN 1 ELSE 0 END) AS live_17301768,
    SUM(CASE WHEN operationtype < 2 AND pipe_type <> 17301768 THEN 1 ELSE 0 END) AS live_other,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_all
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);
```

# DDW BIM Group — JSearchMK에서 PocTrace로 옮긴 것

작성일: 2026-11-23. 출처는 `D:\JCS\1. Codes\260718\JSearchMK\1. Latest Source Code`의 `FrmMonamiQueryBuilder`, `FrmMonamiHistoryQueryBuilder`, `docs/Verify.X2M.monami.ko.md`다. 접목한 코드는 `DdwSql.SelectBimGroups`와 Admin `DDW Cache (x2_m)` 버튼이다.

## 1. 스키마에서 쓰는 사실

DDW 원본은 Linked Server다. 접속 카탈로그는 `master`여도 되고, 데이터는 `remote_monami.monami.dbo.tb_*`다. PocTrace는 이 앞부분을 `DdwLine.CatalogPrefix()`로 만든다. 기본은 `[remote_monami].[monami]`이고, `DdwSql`이 `.dbo.테이블`을 붙인다. 캐시 폴더 키는 `x2_m`이라 카탈로그 이름과 다르다.

live 행은 `version_sequence < 1000000000`이다. 그 이상이면 삭제다. `operationtype` 열은 이 카탈로그에 없다. id 열은 `varbinary(16)`이라 `TRY_CONVERT(uniqueidentifier, …)`로 읽는다. `nvarchar`로 바꾸면 글자가 깨진다.

JSearchMK가 BIM Group에 쓰는 테이블은 네 개다.

| 테이블 | 역할 | 대략 규모 |
|---|---|---|
| `tb_instancegroups` | 그룹 id, 이름, 부모 id, 종류 | 약 9만 |
| `tb_instancegroupmembers` | 그룹 id ↔ 인스턴스 id | 약 2천5백만 |
| `tb_modelinstances` / `tb_modeltemplates` / `tb_utilities` | 멤버의 이름, 좌표, Utility | 모델 약 950만 |
| `tb_attributes` | `Element ID_Number`, `ConnectionTag`, `BIM_DESIGN_INFO`, `S5_LATERAL NUMBER` | 1억 행 이상 |

그룹 열 대응은 다음과 같다.

| JSearchMK | DDW 열 | PocTrace `BimGroupRecord` |
|---|---|---|
| 그룹 id | `instance_group_id` | `GroupId` |
| `BIMGroupName` | `instance_group_nm` | `Name` |
| 부모 | `instance_group_parent_id` | `ParentId`. 없거나 변환 실패면 zero GUID |
| capa 제외 | `instance_group_nm NOT LIKE '%capa%'` | 같은 조건 |
| 삭제 제외 | `version_sequence < 1000000000` | 그룹 행에만 적용 |

S5D의 `ConfigurationGroupLog`는 `GroupID`, `Name`, `ParentID`, `OperationType < 2`다. 같은 id는 `OPTIME` 최신 1건이다. DDW 그룹은 live 필터만 쓰고, 이력 테이블(`tb_instanceaabbs_hist`)을 조인하지 않는다.

## 2. JSearchMK가 그룹을 가져오는 방식

Verify와 Compare는 열린 Revit 파일의 BIM Group 이름 하나로 조회한다. `FrmMonamiQueryBuilder.BuildNozzleQuery`의 형태다.

1. `tb_instancegroups.instance_group_nm LIKE '%파일이름%'` 을 원격 `WHERE`에 리터럴로 둔다.
2. capa를 빼면 `NOT LIKE '%capa%'`도 같은 `WHERE`에 둔다.
3. `tb_instancegroupmembers`를 그 그룹에만 조인한다. 그룹 멤버가 기준이라, 템플릿이 없는 날배관도 한 행으로 남는다. 모델·템플릿·Utility는 `LEFT JOIN`이다.
4. 속성은 `attribute_nm`이 고정된 네 덩어리로 나누고, 원격 `WHERE`에 이름 리터럴을 둔다. 속성 테이블 전체를 끌어오지 않는다.
5. 원격 테이블을 로컬 임시 테이블과 조인하지 않는다. 조인하면 원격 전체가 넘어와 수 분이 걸린다.

이력 조회(`FrmMonamiHistoryQueryBuilder`)는 다른 일이다. 대상 `instance_id`만 200개씩 `IN`으로 걸어 `tb_instancegroupmembers`에 그 id가 있는지 본다. 그룹 트리 전체를 만들지 않는다.

이 조회는 파일 하나의 노즐·Utility 비교용이다. PocTrace 캐시 작성이 아니다.

## 3. PocTrace에 붙인 것

Admin의 DDW Cache 버튼이 그래프를 `GraphCache_x2_m`에 쓴 뒤, 같은 연결로 `DdwSql.SelectBimGroups`를 실행해 `BimGroupCache_x2_m`에 저장한다. 그룹 저장이 실패해도 그래프 파일은 지우지 않는다. S5D 캐시 갱신은 계속 `ConfigurationGroupLog`만 읽는다. `x2_m`은 그 경로에서 빠져 있다.

사이드카 SQL은 그룹 테이블만 읽는다.

- live, 이름 공백 제외, capa 제외
- `GroupId`, `Name`, `ParentId` 세 열. 기존 v2 캐시 형식 그대로
- `tb_instancegroupmembers`, `tb_attributes` 조인 없음
- 노드 `GetSelectNodes`에 그룹을 중첩하지 않음

경로 계산은 기존 `BimGroupNameIndex`다. 이름이 EQPID와 같은 그룹을 찾고, `ParentId`를 `GroupId`로 따라 올라가 `부모\…\이름`을 만든다.

SSMS로 같은 문장을 보려면 `DdwSql.ReviewScript`의 14번 배치다. 09~13이 그래프이고 14가 이 트리다.

## 4. 일부러 옮기지 않은 것

JSearchMK의 `LIKE '%그룹명%'`과 멤버 조인은 캐시 SQL에 넣지 않았다. 캐시는 라인 전체의 그룹 트리다. 파일 하나의 멤버 목록은 2천5백만 멤버를 그래프 노드에 붙이는 일이 된다.

그래서 지금 사이드카는 S5D 철거 Group 시트와 형식이 같다. S5D에서는 그룹 `Name`이 EQPID다. DDW의 `instance_group_nm`은 Revit BIM Group 이름이다. 설비 Tag와 문자열이 같지 않으면 철거 시트의 경로 매칭은 되지 않는다. 설비와 그룹을 잇는 키는 `tb_instancegroupmembers.instance_id`인데, 그 매핑은 캐시 형식을 바꾸는 일이라 이번 범위 밖에 두었다.

속성으로 BIMID를 만드는 규칙, 노즐 템플릿 필터, 이력 200건 분할도 JSearchMK에 남긴다. 그래프 캐시의 속성 이름은 기존 `DdwSql.SelectAttributes` 목록을 유지한다.

## 5. 확인

SVDI에서 14번 배치를 실행해 행 수를 확인하는 일은 아직 하지 않았다. 그룹이 약 9만이라 전체 조회는 가능하지만, 이 환경에는 monami 연결이 없다.

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

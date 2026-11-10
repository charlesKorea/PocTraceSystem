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

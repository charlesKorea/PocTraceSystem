-- 09 BIM Group 모양. 보통. tb_instancegroups 는 약 9만 행.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- capa 제외와 부모 열이 sidecar 에 필요한지 확인한다.
-- 그룹 이름은 앞 24자만.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*) AS all_rows,
    SUM(CASE WHEN instance_group_nm LIKE '%capa%' THEN 1 ELSE 0 END) AS capa_rows,
    SUM(CASE WHEN (instance_group_parent_id IS NULL
         OR instance_group_parent_id = '00000000-0000-0000-0000-000000000000') THEN 1 ELSE 0 END) AS parent_null_rows,
    SUM(CASE WHEN (instance_group_nm IS NULL OR LTRIM(RTRIM(instance_group_nm)) = '') THEN 1 ELSE 0 END) AS name_blank_rows
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
WHERE instance_group_nm NOT LIKE '%capa%'
ORDER BY instance_group_nm;

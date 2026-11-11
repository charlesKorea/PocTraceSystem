-- 08 작은 마스터의 모양. 빠름.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- 사람 이름, 로그인, 비밀번호, 전화, 메일은 읽지 않는다.
-- 회사명은 값 대신 채워진 행수만 센다.
-- 결과 그리드를 모두 복사한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*) AS utility_rows
FROM remote_monami.monami.dbo.tb_utilities WITH (NOLOCK);

SELECT TOP 20
    LEFT(utility_nm, 24) AS utility24,
    utility_type
FROM remote_monami.monami.dbo.tb_utilities WITH (NOLOCK)
ORDER BY utility_nm;

SELECT
    COUNT(*) AS group_rows
FROM remote_monami.monami.dbo.tb_utility_groups WITH (NOLOCK);

SELECT TOP 20
    LEFT(utility_group_nm, 24) AS group24
FROM remote_monami.monami.dbo.tb_utility_groups WITH (NOLOCK)
ORDER BY utility_group_nm;

SELECT TOP 15
    LEFT(pipesize_nm, 24) AS size24,
    pipesize_type,
    ROUND(diameter, 3) AS diameter,
    ROUND(width, 3) AS width,
    ROUND(height, 3) AS height
FROM remote_monami.monami.dbo.tb_pipesize WITH (NOLOCK)
ORDER BY pipesize_nm;

SELECT
    poc_type,
    LEFT(poc_type_value, 40) AS value40
FROM remote_monami.monami.dbo.tb_poctypes WITH (NOLOCK)
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

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

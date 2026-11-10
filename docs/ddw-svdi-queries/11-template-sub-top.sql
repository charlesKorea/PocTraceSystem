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

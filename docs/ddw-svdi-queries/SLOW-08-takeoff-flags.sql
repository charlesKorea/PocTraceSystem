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

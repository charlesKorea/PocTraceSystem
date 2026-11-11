-- SLOW-08 Takeoff 후보. 템플릿 TAKEOFF_YN 과 배관 유형을 따로 센다.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- TAKEOFF_YN 만으로 Takeoff 를 분류하지 않는다. 두 수의 차이를 보기 위한 조회다.
-- 템플릿과 배관을 한 조인으로 붙이지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    takeoff_yn,
    COUNT(*) AS template_rows
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK)
GROUP BY takeoff_yn
ORDER BY takeoff_yn;

SELECT
    equipment_yn,
    COUNT(*) AS template_rows
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK)
GROUP BY equipment_yn
ORDER BY equipment_yn;

SELECT
    SUM(CASE WHEN pipe_type = 17301768 THEN 1 ELSE 0 END) AS rows_17301768,
    SUM(CASE WHEN pipe_type <> 17301768 THEN 1 ELSE 0 END) AS rows_other,
    COUNT(*) AS rows_all
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

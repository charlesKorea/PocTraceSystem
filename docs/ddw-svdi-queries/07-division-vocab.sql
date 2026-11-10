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

-- SLOW-03 POC owner 와 배관 끝점 개수. POC 테이블 집계.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- owner_type 768 은 모델 POC, 256 은 배관 끝점이다.
-- 끝점 히스토그램은 1~6개와 90개 이상만 돌려 준다. 4개 이상 합계와 최댓값은 한 줄이다.
-- 10분이 넘으면 TIMEOUT. 끝점 쿼리가 더 길다. 첫 결과(owner_type)만이라도 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    owner_type,
    COUNT(*) AS rows_all
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
GROUP BY owner_type
ORDER BY owner_type;

SELECT
    SUM(CASE WHEN n = 1 THEN 1 ELSE 0 END) AS pipes_1,
    SUM(CASE WHEN n = 2 THEN 1 ELSE 0 END) AS pipes_2,
    SUM(CASE WHEN n = 3 THEN 1 ELSE 0 END) AS pipes_3,
    SUM(CASE WHEN n >= 4 THEN 1 ELSE 0 END) AS pipes_4plus,
    MAX(n) AS max_ends
FROM (
    SELECT owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE owner_type = 256
    GROUP BY owner_instance_id
) AS s;

SELECT
    n AS endpoint_n,
    COUNT(*) AS pipes
FROM (
    SELECT owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE owner_type = 256
    GROUP BY owner_instance_id
) AS s
WHERE n <= 6 OR n >= 90
GROUP BY n
ORDER BY n;

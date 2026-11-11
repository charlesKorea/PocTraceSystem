-- SLOW-04 연결과 순번 품질. POC 집계, 이어서 원격끼리 LEFT JOIN.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- dangling 조인은 가장 길다. 앞의 두 결과가 나온 뒤 10분이 더 넘으면 dangling 만 취소한다.
-- 역방향 TOP 1 은 만들지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    owner_type,
    SUM(CASE WHEN connected_poc_id IS NULL THEN 1 ELSE 0 END) AS connected_null,
    SUM(CASE WHEN connected_poc_id = '00000000-0000-0000-0000-000000000000' THEN 1 ELSE 0 END) AS connected_zero,
    SUM(CASE WHEN connected_poc_id IS NOT NULL
              AND connected_poc_id <> '00000000-0000-0000-0000-000000000000' THEN 1 ELSE 0 END) AS connected_set
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE owner_type IN (256, 768)
GROUP BY owner_type
ORDER BY owner_type;

SELECT
    COUNT(*) AS owners_with_duplicate_order
FROM (
    SELECT owner_instance_id
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE owner_type = 256
    GROUP BY owner_instance_id, connection_order
    HAVING COUNT(*) > 1
) AS d;

SELECT
    COUNT(*) AS dangling_pipe_pocs
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_pocinstances AS t WITH (NOLOCK)
    ON t.instance_id = p.connected_poc_id
WHERE p.owner_type = 256
  AND p.connected_poc_id IS NOT NULL
  AND p.connected_poc_id <> '00000000-0000-0000-0000-000000000000'
  AND t.instance_id IS NULL;

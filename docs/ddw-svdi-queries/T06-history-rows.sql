-- T06 instance_id 하나에 행이 여러 개인지(이력 행). 보통에서 느림.
-- S5D 의 로그 테이블은 최신 행을 골라야 했다. DDW 의 tb_* 는 instance_id 당 한 행인지, 삭제가 새 행인지 제자리 갱신인지 본다.
-- live 필터 없이 전체 행 기준이다. 10분이 넘으면 그 블록만 취소하고 TIMEOUT 과 테이블 이름을 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'tb_modelinstances' AS src,
    COUNT(*) AS instance_ids,
    SUM(CASE WHEN n > 1 THEN 1 ELSE 0 END) AS ids_with_many_rows,
    MAX(n) AS max_rows_per_id,
    SUM(CASE WHEN n > 1 AND live_n > 0 AND deleted_n > 0 THEN 1 ELSE 0 END) AS ids_live_and_deleted
FROM (
    SELECT instance_id,
           COUNT(*) AS n,
           SUM(CASE WHEN version_sequence < 1000000000 THEN 1 ELSE 0 END) AS live_n,
           SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS deleted_n
    FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
    GROUP BY instance_id
) AS s;

SELECT
    'tb_pipeinstances' AS src,
    COUNT(*) AS instance_ids,
    SUM(CASE WHEN n > 1 THEN 1 ELSE 0 END) AS ids_with_many_rows,
    MAX(n) AS max_rows_per_id,
    SUM(CASE WHEN n > 1 AND live_n > 0 AND deleted_n > 0 THEN 1 ELSE 0 END) AS ids_live_and_deleted
FROM (
    SELECT instance_id,
           COUNT(*) AS n,
           SUM(CASE WHEN version_sequence < 1000000000 THEN 1 ELSE 0 END) AS live_n,
           SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS deleted_n
    FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
    GROUP BY instance_id
) AS s;

SELECT
    'tb_pocinstances' AS src,
    COUNT(*) AS instance_ids,
    SUM(CASE WHEN n > 1 THEN 1 ELSE 0 END) AS ids_with_many_rows,
    MAX(n) AS max_rows_per_id,
    SUM(CASE WHEN n > 1 AND live_n > 0 AND deleted_n > 0 THEN 1 ELSE 0 END) AS ids_live_and_deleted
FROM (
    SELECT instance_id,
           COUNT(*) AS n,
           SUM(CASE WHEN version_sequence < 1000000000 THEN 1 ELSE 0 END) AS live_n,
           SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS deleted_n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    GROUP BY instance_id
) AS s;

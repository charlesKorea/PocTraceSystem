-- SLOW-01 live 조건 비교. 모델, 배관, POC 를 각각 한 번씩 센다.
-- operationtype < 2 와 version_sequence < 1000000000 이 같은 행인지 본다.
-- 한 결과 집합이 끝난 뒤 다음이 시작된다. 각 블록이 10분을 넘으면 그 블록만 취소하고 TIMEOUT 과 테이블 이름을 보낸다.
-- 분포가 맞는지 확인하기 전까지만 쓰는 조회다. 캐시 SQL에 두 조건을 섞지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'tb_modelinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK);

SELECT
    'tb_pipeinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

SELECT
    'tb_pocinstances' AS src,
    SUM(CASE WHEN operationtype < 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS both_live,
    SUM(CASE WHEN operationtype < 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS ot_live_ver_not,
    SUM(CASE WHEN operationtype >= 2 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS ot_not_ver_live,
    SUM(CASE WHEN operationtype >= 2 AND (version_sequence IS NULL OR version_sequence >= 1000000000) THEN 1 ELSE 0 END) AS both_not,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK);

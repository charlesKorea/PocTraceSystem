-- SLOW-01 version_sequence 분포. 모델 102만, POC 147만, 배관 23만 행을 각각 한 번씩 집계한다(02 결과).
-- rev3 2026-11-11: 모든 tb_* 에 version_sequence 가 있고 operationtype 은 없다(담당자 확인).
-- rev4 2026-11-12: 담당자가 모든 테이블 기본 조건으로 version_sequence < 1000000000 을 확정했다.
-- 이 분포는 확정된 임계 1e9 가 실제 값 사이의 틈에 있는지(1e7 와 1e9 사이에 값이 있는지) 검증하는 용도다.
-- 테이블당 집계 쿼리 두 개씩이다. 10분이 넘으면 그 블록만 취소하고 TIMEOUT 과 테이블 이름을 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'tb_modelinstances' AS src,
    SUM(CASE WHEN version_sequence IS NULL THEN 1 ELSE 0 END) AS v_null,
    SUM(CASE WHEN version_sequence < 10000000 THEN 1 ELSE 0 END) AS v_lt_1e7,
    SUM(CASE WHEN version_sequence >= 10000000 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS v_1e7_to_1e9,
    SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS v_ge_1e9,
    MIN(version_sequence) AS v_min,
    MAX(version_sequence) AS v_max,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK);

SELECT
    'tb_modelinstances' AS src,
    CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END AS bucket,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
GROUP BY CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END
ORDER BY MIN(version_sequence);

SELECT
    'tb_pipeinstances' AS src,
    SUM(CASE WHEN version_sequence IS NULL THEN 1 ELSE 0 END) AS v_null,
    SUM(CASE WHEN version_sequence < 10000000 THEN 1 ELSE 0 END) AS v_lt_1e7,
    SUM(CASE WHEN version_sequence >= 10000000 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS v_1e7_to_1e9,
    SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS v_ge_1e9,
    MIN(version_sequence) AS v_min,
    MAX(version_sequence) AS v_max,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

SELECT
    'tb_pipeinstances' AS src,
    CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END AS bucket,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
GROUP BY CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END
ORDER BY MIN(version_sequence);

SELECT
    'tb_pocinstances' AS src,
    SUM(CASE WHEN version_sequence IS NULL THEN 1 ELSE 0 END) AS v_null,
    SUM(CASE WHEN version_sequence < 10000000 THEN 1 ELSE 0 END) AS v_lt_1e7,
    SUM(CASE WHEN version_sequence >= 10000000 AND version_sequence < 1000000000 THEN 1 ELSE 0 END) AS v_1e7_to_1e9,
    SUM(CASE WHEN version_sequence >= 1000000000 THEN 1 ELSE 0 END) AS v_ge_1e9,
    MIN(version_sequence) AS v_min,
    MAX(version_sequence) AS v_max,
    COUNT(*) AS all_rows
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK);

SELECT
    'tb_pocinstances' AS src,
    CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END AS bucket,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
GROUP BY CASE
        WHEN version_sequence IS NULL THEN 'null'
        WHEN version_sequence < 10000000 THEN '<1e7'
        WHEN version_sequence < 1000000000 THEN '1e7..1e9'
        WHEN version_sequence < 10000000000 THEN '1e9..1e10'
        ELSE '>=1e10'
    END
ORDER BY MIN(version_sequence);

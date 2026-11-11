-- SLOW-01 version_sequence 분포. 모델 테이블 한 번 집계.
-- rev2 2026-11-11: operationtype 열이 없는 DDW다. 삭제 판정은 version_sequence 임계로 한다.
-- 임계가 1000000000(JSearchMK 주석)인지 10000000(SVDI 담당 설명)인지 이 분포로 가른다.
-- 01B 에서 has_version_sequence=1 인 테이블만 같은 블록을 복사해 테이블 이름을 바꿔 돌린다.
-- 10분이 넘으면 TIMEOUT 과 테이블 이름을 보낸다.
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

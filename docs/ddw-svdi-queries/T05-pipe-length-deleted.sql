-- T05 배관 길이 열과 삭제 행. 보통. 배관 228,878 행.
-- 현재 캐시 코드는 length1 을 쓴다. 담당자 설명은 length2 가 배관 길이(mm)다. 둘의 차이를 본다.
-- 삭제 행(version_sequence 1e9 이상)이 얼마나 남아 있고 언제 지워졌는지 본다. last_update 가 없으면 에러가 난다. 그 경우 에러만 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- T05-A pipe_type 별 length1, length2 비교(live)
SELECT
    pipe_type,
    COUNT(*) AS rows_live,
    SUM(CASE WHEN length1 IS NULL OR length1 <= 0 THEN 1 ELSE 0 END) AS length1_empty,
    SUM(CASE WHEN length2 IS NULL OR length2 <= 0 THEN 1 ELSE 0 END) AS length2_empty,
    SUM(CASE WHEN length1 > 0 AND length2 > 0 AND ABS(length1 - length2) > 0.5 THEN 1 ELSE 0 END) AS differ_over_half_mm,
    SUM(CAST(CASE WHEN length1 > 0 THEN length1 ELSE 0 END AS float)) AS sum_length1,
    SUM(CAST(CASE WHEN length2 > 0 THEN length2 ELSE 0 END AS float)) AS sum_length2
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE version_sequence < 1000000000
GROUP BY pipe_type
ORDER BY pipe_type;

-- T05-B 두 값이 다른 행 샘플 8개. id 는 앞 10자만.
SELECT TOP 8
    LEFT(CONVERT(varchar(34), CONVERT(varbinary(16), instance_id), 1), 10) AS id10,
    pipe_type,
    ROUND(length1, 3) AS length1,
    ROUND(length2, 3) AS length2
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE version_sequence < 1000000000
  AND length1 > 0 AND length2 > 0
  AND ABS(length1 - length2) > 0.5;

-- T05-C 삭제 행 건수와 마지막 갱신 시각 범위
SELECT
    COUNT(*) AS rows_deleted,
    MIN(last_update) AS min_last_update,
    MAX(last_update) AS max_last_update
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE version_sequence >= 1000000000;

-- T05-D 삭제 행의 pipe_type 별 건수와 길이 합(length2)
SELECT
    pipe_type,
    COUNT(*) AS rows_deleted,
    SUM(CAST(CASE WHEN length2 > 0 THEN length2 ELSE 0 END AS float)) AS sum_length2
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE version_sequence >= 1000000000
GROUP BY pipe_type
ORDER BY pipe_type;

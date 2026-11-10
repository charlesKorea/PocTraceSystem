-- SLOW-05 길이, 구경, 좌표의 크기. 단위가 mm 인지 m 인지 본다.
-- 값은 최소, 평균, 최대만. 좌표 원문은 행으로 내지 않는다.
-- 배관과 모델을 각각 집계한다. 10분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    'pipe.length1' AS src,
    MIN(length1) AS min_v,
    AVG(length1) AS avg_v,
    MAX(length1) AS max_v
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND length1 IS NOT NULL
  AND length1 > 0;

SELECT
    'pipe.diameter1' AS src,
    MIN(diameter1) AS min_v,
    AVG(diameter1) AS avg_v,
    MAX(diameter1) AS max_v
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND diameter1 IS NOT NULL
  AND diameter1 > 0;

SELECT
    'poc.diameter' AS src,
    MIN(diameter) AS min_v,
    AVG(diameter) AS avg_v,
    MAX(diameter) AS max_v
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE operationtype < 2
  AND owner_type = 768
  AND diameter IS NOT NULL
  AND diameter > 0;

SELECT
    'model.pos' AS src,
    MIN(posz) AS min_z,
    AVG(posz) AS avg_z,
    MAX(posz) AS max_z,
    MIN(posy) AS min_y,
    MAX(posy) AS max_y,
    SUM(CASE WHEN ROUND(posz, 0) < 100000 THEN 1 ELSE 0 END) AS posz_under_100000,
    SUM(CASE WHEN ROUND(posy, 0) < 100000 THEN 1 ELSE 0 END) AS posy_under_100000,
    COUNT(*) AS live_models
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
WHERE operationtype < 2;

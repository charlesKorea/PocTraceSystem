-- pump fore-line POC 와 middle foreline 피팅의 X, Y 1:1.
-- 높이 posz 는 비교하지 않는다. 명칭은 대소문자를 무시한다.
--
-- pump: Subdivision1 = 4-pcs, Subdivision2 = pump
-- 진공 POC: 그 설비의 owner_type 768 POC 이고 POC template name 에 fore-line 이 있다.
--   POC 좌표는 tb_pocinstances.posx, posy 이다.
-- middle foreline: Subdivision1 = 기타, Subdivision2 = middle foreline,
--   객체명에 foreline 이 있다. 좌표는 tb_modelinstances.posx, posy 이다.
--
-- 판정 키는 ROUND(x, 1), ROUND(y, 1) 이다. 캐시 v15 POC 좌표, v14 모델 좌표와 같다.
-- 같은 키에 진공 POC 1개와 middle foreline 1개만 있으면 직선 대응이다.
-- 피팅이 없거나, 피팅이 둘 이상이거나, 진공 POC가 둘 이상이면 그 POC 는 불일치다.
-- pump 에 진공 POC 가 여러 개여도 POC 마다 따로 본다.
--
-- 전체 pump 와 전체 피팅을 곱하지 않는다.
-- 이름 조건으로 두 집합을 먼저 줄이고, 좌표 키로 묶은 뒤 그 묶음의 개수만 비교한다.
--
-- 예. middle foreline (207669.97, 47104.53), (207669.97, 46604.53)
--     POC (207666.97, 47054.53) 는 같은 키가 없다.
--     POC (207669.97, 46604.53) 는 두 번째 피팅과 1:1 이다.
--     그 pump 는 첫 결과 집합에 남고, 둘째 결과에서 POC 별 상태가 보인다.
--
-- 연결 서버나 카탈로그가 다르면 remote_monami.monami 를 바꾼다.
-- live 는 version_sequence < 1000000000 이다.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#model') IS NOT NULL DROP TABLE #model;
IF OBJECT_ID('tempdb..#pump') IS NOT NULL DROP TABLE #pump;
IF OBJECT_ID('tempdb..#vac') IS NOT NULL DROP TABLE #vac;
IF OBJECT_ID('tempdb..#mid') IS NOT NULL DROP TABLE #mid;
IF OBJECT_ID('tempdb..#fit_at') IS NOT NULL DROP TABLE #fit_at;
IF OBJECT_ID('tempdb..#poc_at') IS NOT NULL DROP TABLE #poc_at;

SELECT
    src.instance_id,
    src.tag,
    src.instance_nm,
    src.sub1,
    src.sub2,
    src.xk,
    src.yk,
    src.posz
INTO #model
FROM (
SELECT
    mi.INSTANCE_ID AS instance_id,
    CASE
        WHEN mt.BIM_YN = 0 THEN CONVERT(nvarchar(512), mi.[tag])
        WHEN mt.BIM_YN = 1 THEN COALESCE(NULLIF(CONVERT(nvarchar(512), mi.[s5_eqcode]), N''), CONVERT(nvarchar(512), mi.[tag]))
        ELSE CONVERT(nvarchar(512), mi.[tag])
    END AS tag,
    CONVERT(nvarchar(512), mi.INSTANCE_NM) AS instance_nm,
    LOWER(COALESCE(NULLIF(CONVERT(nvarchar(256), mt.[Subdivision_1_name]), N''), CONVERT(nvarchar(256), sub1.DIVISION_VALUE))) AS sub1,
    LOWER(COALESCE(NULLIF(CONVERT(nvarchar(256), mt.[Subdivision_2_name]), N''), CONVERT(nvarchar(256), sub2.DIVISION_VALUE))) AS sub2,
    ROUND(mi.POSX, 1) AS xk,
    ROUND(mi.POSY, 1) AS yk,
    mi.POSZ AS posz
FROM remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_modeltemplates AS mt WITH (NOLOCK)
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID
   AND mt.version_sequence < 1000000000
LEFT JOIN remote_monami.monami.dbo.tb_division AS sub1 WITH (NOLOCK)
    ON sub1.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_1_ID)
   AND sub1.version_sequence < 1000000000
LEFT JOIN remote_monami.monami.dbo.tb_division AS sub2 WITH (NOLOCK)
    ON sub2.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_2_ID)
   AND sub2.version_sequence < 1000000000
WHERE mi.version_sequence < 1000000000
) AS src
WHERE (src.sub1 = N'4-pcs' AND src.sub2 = N'pump')
   OR (
        src.sub1 = N'기타'
        AND src.sub2 = N'middle foreline'
        AND LOWER(ISNULL(src.instance_nm, N'')) LIKE N'%foreline%'
      );

SELECT instance_id, tag, instance_nm
INTO #pump
FROM #model
WHERE sub1 = N'4-pcs'
  AND sub2 = N'pump';

CREATE CLUSTERED INDEX ix_pump ON #pump(instance_id);

SELECT
    pump.instance_id AS pump_id,
    pump.tag AS pump_tag,
    pump.instance_nm AS pump_name,
    p.INSTANCE_ID AS poc_id,
    CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM) AS poc_name,
    ROUND(p.POSX, 1) AS xk,
    ROUND(p.POSY, 1) AS yk,
    p.POSZ AS posz
INTO #vac
FROM #pump AS pump
JOIN remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
    ON p.OWNER_INSTANCE_ID = pump.instance_id
   AND p.version_sequence < 1000000000
   AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768
JOIN remote_monami.monami.dbo.tb_poctemplates AS pt WITH (NOLOCK)
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID
   AND pt.version_sequence < 1000000000
WHERE LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) LIKE N'%fore-line%';

CREATE CLUSTERED INDEX ix_vac_xy ON #vac(xk, yk);

SELECT instance_id, tag, instance_nm, xk, yk, posz
INTO #mid
FROM #model
WHERE sub1 = N'기타'
  AND sub2 = N'middle foreline'
  AND LOWER(ISNULL(instance_nm, N'')) LIKE N'%foreline%'
  AND xk IS NOT NULL
  AND yk IS NOT NULL;

CREATE CLUSTERED INDEX ix_mid_xy ON #mid(xk, yk);

SELECT xk, yk, COUNT(*) AS fit_count
INTO #fit_at
FROM #mid
GROUP BY xk, yk;

CREATE UNIQUE CLUSTERED INDEX ix_fit_at ON #fit_at(xk, yk);

SELECT xk, yk, COUNT(*) AS poc_count
INTO #poc_at
FROM #vac
WHERE xk IS NOT NULL AND yk IS NOT NULL
GROUP BY xk, yk;

CREATE UNIQUE CLUSTERED INDEX ix_poc_at ON #poc_at(xk, yk);

IF OBJECT_ID('tempdb..#judged') IS NOT NULL DROP TABLE #judged;

SELECT
    v.pump_id,
    v.pump_tag,
    v.pump_name,
    v.poc_id,
    v.poc_name,
    v.xk AS poc_x,
    v.yk AS poc_y,
    v.posz AS poc_z,
    ISNULL(f.fit_count, 0) AS fitting_count_at_xy,
    pa.poc_count AS poc_count_at_xy,
    CASE
        WHEN v.xk IS NULL OR v.yk IS NULL THEN N'no coordinate'
        WHEN ISNULL(f.fit_count, 0) = 0 THEN N'no middle foreline'
        WHEN f.fit_count > 1 THEN N'multiple middle foreline'
        WHEN pa.poc_count > 1 THEN N'multiple vacuum poc'
        ELSE N'match'
    END AS align_result
INTO #judged
FROM #vac AS v
LEFT JOIN #fit_at AS f
    ON f.xk = v.xk AND f.yk = v.yk
LEFT JOIN #poc_at AS pa
    ON pa.xk = v.xk AND pa.yk = v.yk;

-- 1. 진공 POC 중 하나라도 1:1 이 아닌 pump.
SELECT
    pump_id,
    pump_tag,
    pump_name,
    COUNT(*) AS vacuum_poc_count,
    SUM(CASE WHEN align_result = N'match' THEN 1 ELSE 0 END) AS matched_count,
    SUM(CASE WHEN align_result = N'match' THEN 0 ELSE 1 END) AS unmatched_count
FROM #judged
GROUP BY pump_id, pump_tag, pump_name
HAVING SUM(CASE WHEN align_result = N'match' THEN 0 ELSE 1 END) > 0
ORDER BY pump_tag, pump_name;

-- 2. 그 pump 의 진공 POC 전부. 맞는 POC 와 어긋난 POC 가 같이 보인다.
SELECT
    j.pump_tag,
    j.pump_name,
    j.poc_id,
    j.poc_name,
    j.poc_x,
    j.poc_y,
    j.poc_z,
    j.align_result,
    j.fitting_count_at_xy,
    j.poc_count_at_xy
FROM #judged AS j
WHERE EXISTS (
    SELECT 1
    FROM #judged AS bad
    WHERE bad.pump_id = j.pump_id
      AND bad.align_result <> N'match'
)
ORDER BY j.pump_tag, j.poc_name, j.poc_x, j.poc_y;

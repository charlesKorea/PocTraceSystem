-- pump fore-line POC 와 middle foreline 피팅의 X, Y 1:1.
-- 높이 posz 는 비교하지 않는다. 명칭은 대소문자를 무시한다.
--
-- 이 카탈로그는 monami TbStar 다. 캐시 DdwSql.SelectModels(TbStar) 와 SelectAttributes 와 같은 출처를 쓴다.
-- tb_modelinstances, tb_pocinstances 에 아래 열은 없다. 읽으면 열 오류가 난다.
--   tag, s5_eqcode, construction_code, elementId_number
-- tb_modeltemplates 의 Subdivision_1_name, Subdivision_2_name 도 없다.
-- tb_pocinstances 의 tag, connectionTag 도 없다.
--
-- Subdivision1, Subdivision2 는 템플릿의 subdivision id 를 tb_division.division_value 로 읽는다.
-- tag 는 tb_attributes.attribute_nm = 'tag' 이다.
-- 건설코드는 tb_attributes.attribute_nm = 'Construction-code' 이다.
-- 두 속성의 owner_id 가 그 객체 id 다. 캐시는 인스턴스 열이 비면 이 속성으로 채운다.
-- 속성은 약 1천만 행이다. attribute_nm IN 으로 먼저 줄이고, 줄어든 pump id 와만 맞춘다.
-- 모델 전체에 속성을 상관 조인하지 않는다.
--
-- 이후 배관검수도 같은 출처를 쓴다.
--   배관 번호 S5_LATERAL_NUMBER, hookup HookupNumber, POC tag, ConnectionTag 는 tb_attributes 다.
--   설비와 피팅의 Subdivision 은 tb_division 이다.
--   좌표만 tb_modelinstances, tb_pocinstances 의 posx, posy, posz 다.
--
-- pump: Subdivision1 = 4-pcs, Subdivision2 = pump
--   설비명에 _LL_ 이 있으면 로드락 펌프라 모수에서 뺀다. 대소문자는 무시한다.
-- 진공 POC: 그 설비의 owner_type 768 POC 이고 POC template name 에 fore-line 이 있다.
--   POC 이름에 (LL) 또는 (PS) 가 있으면 그 POC 는 모수에서 뺀다. 대소문자는 무시한다.
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
--     그 pump 의 어긋난 POC 한 행이 결과에 남는다.
--     pump 이름, tag, construction code, 좌표가 같이 있다.
--
-- 연결 서버나 카탈로그가 다르면 remote_monami.monami 를 바꾼다.
-- live 는 version_sequence < 1000000000 이다.
-- 같은 owner 에 tag 또는 Construction-code 가 둘 이상이면 값 하나만 남긴다.
-- 캐시는 먼저 읽은 값을 두고 중복을 센다.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#model') IS NOT NULL DROP TABLE #model;
IF OBJECT_ID('tempdb..#pump') IS NOT NULL DROP TABLE #pump;
IF OBJECT_ID('tempdb..#attr_named') IS NOT NULL DROP TABLE #attr_named;
IF OBJECT_ID('tempdb..#pump_attr') IS NOT NULL DROP TABLE #pump_attr;
IF OBJECT_ID('tempdb..#vac') IS NOT NULL DROP TABLE #vac;
IF OBJECT_ID('tempdb..#mid') IS NOT NULL DROP TABLE #mid;
IF OBJECT_ID('tempdb..#fit_at') IS NOT NULL DROP TABLE #fit_at;
IF OBJECT_ID('tempdb..#poc_at') IS NOT NULL DROP TABLE #poc_at;
IF OBJECT_ID('tempdb..#judged') IS NOT NULL DROP TABLE #judged;

SELECT
    src.instance_id,
    src.instance_nm,
    src.sub1,
    src.sub2,
    src.xk,
    src.yk,
    src.posz
INTO #model
FROM (
SELECT
    TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID) AS instance_id,
    COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)) AS instance_nm,
    LOWER(CONVERT(nvarchar(256), sub1.DIVISION_VALUE)) AS sub1,
    LOWER(CONVERT(nvarchar(256), sub2.DIVISION_VALUE)) AS sub2,
    ROUND(mi.POSX, 1) AS xk,
    ROUND(mi.POSY, 1) AS yk,
    ROUND(mi.POSZ, 1) AS posz
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
WHERE src.instance_id IS NOT NULL
  AND (
        (src.sub1 = N'4-pcs' AND src.sub2 = N'pump')
        OR (
            src.sub1 = N'기타'
            AND src.sub2 = N'middle foreline'
            AND LOWER(ISNULL(src.instance_nm, N'')) LIKE N'%foreline%'
        )
      );

SELECT instance_id, instance_nm, xk, yk, posz
INTO #pump
FROM #model
WHERE sub1 = N'4-pcs'
  AND sub2 = N'pump'
  AND LOWER(ISNULL(instance_nm, N'')) NOT LIKE N'%\_ll\_%' ESCAPE N'\';

CREATE CLUSTERED INDEX ix_pump ON #pump(instance_id);

-- 이름 필터만 원격에 둔다. pump id 조인은 로컬에서 한다.
SELECT
    TRY_CONVERT(uniqueidentifier, a.OWNER_ID) AS owner_id,
    CONVERT(nvarchar(256), a.ATTRIBUTE_NM) AS attribute_nm,
    LTRIM(RTRIM(CONVERT(nvarchar(512), a.ATTRIBUTE_VAL))) AS attribute_val
INTO #attr_named
FROM remote_monami.monami.dbo.tb_attributes AS a WITH (NOLOCK)
WHERE a.version_sequence < 1000000000
  AND a.ATTRIBUTE_NM IN (N'tag', N'Construction-code')
  AND a.ATTRIBUTE_VAL IS NOT NULL
  AND LTRIM(RTRIM(CONVERT(nvarchar(512), a.ATTRIBUTE_VAL))) <> N'';

SELECT
    a.owner_id,
    MAX(CASE WHEN a.attribute_nm = N'tag' THEN a.attribute_val END) AS tag,
    MAX(CASE WHEN a.attribute_nm = N'Construction-code' THEN a.attribute_val END) AS construction_code
INTO #pump_attr
FROM #attr_named AS a
JOIN #pump AS p
    ON p.instance_id = a.owner_id
WHERE a.owner_id IS NOT NULL
GROUP BY a.owner_id;

DROP TABLE #attr_named;

SELECT
    pump.instance_id AS pump_id,
    attr.tag AS pump_tag,
    pump.instance_nm AS pump_name,
    attr.construction_code AS pump_construction_code,
    pump.xk AS pump_x,
    pump.yk AS pump_y,
    pump.posz AS pump_z,
    TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID) AS poc_id,
    CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM) AS poc_name,
    ROUND(p.POSX, 1) AS xk,
    ROUND(p.POSY, 1) AS yk,
    ROUND(p.POSZ, 1) AS posz
INTO #vac
FROM #pump AS pump
LEFT JOIN #pump_attr AS attr
    ON attr.owner_id = pump.instance_id
JOIN remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
    ON TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID) = pump.instance_id
   AND p.version_sequence < 1000000000
   AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768
JOIN remote_monami.monami.dbo.tb_poctemplates AS pt WITH (NOLOCK)
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID
   AND pt.version_sequence < 1000000000
WHERE LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) LIKE N'%fore-line%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ll)%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ps)%';

CREATE CLUSTERED INDEX ix_vac_xy ON #vac(xk, yk);

SELECT instance_id, instance_nm, xk, yk, posz
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

SELECT
    v.pump_id,
    v.pump_tag,
    v.pump_name,
    v.pump_construction_code,
    v.pump_x,
    v.pump_y,
    v.pump_z,
    v.poc_id,
    v.poc_name,
    v.xk AS poc_x,
    v.yk AS poc_y,
    v.posz AS poc_z,
    ISNULL(f.fit_count, 0) AS fitting_count_at_xy,
    pa.poc_count AS poc_count_at_xy,
    CASE
        WHEN v.xk IS NULL OR v.yk IS NULL THEN N'좌표없음'
        WHEN ISNULL(f.fit_count, 0) = 0 THEN N'middle foreline 없음'
        WHEN f.fit_count > 1 THEN N'middle foreline 여러개'
        WHEN pa.poc_count > 1 THEN N'진공POC 여러개'
        ELSE N'일치'
    END AS align_result
INTO #judged
FROM #vac AS v
LEFT JOIN #fit_at AS f
    ON f.xk = v.xk AND f.yk = v.yk
LEFT JOIN #poc_at AS pa
    ON pa.xk = v.xk AND pa.yk = v.yk;

-- 불일치 POC 한 행. pump 이름, tag, construction code, 좌표와 그 POC 정보를 같이 둔다.
SELECT
    j.align_result,
    j.pump_name,
    j.pump_tag,
    j.pump_construction_code,
    j.pump_x,
    j.pump_y,
    j.pump_z,
    j.poc_id,
    j.poc_name,
    j.poc_x,
    j.poc_y,
    j.poc_z,
    j.fitting_count_at_xy,
    j.poc_count_at_xy
FROM #judged AS j
WHERE j.align_result <> N'일치'
ORDER BY j.pump_tag, j.pump_name, j.poc_name, j.poc_x, j.poc_y;

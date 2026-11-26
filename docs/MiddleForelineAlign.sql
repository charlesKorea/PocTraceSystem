-- pump fore-line POC 와 middle foreline 피팅의 X, Y 1:1.
-- 높이 posz 는 비교하지 않는다. 명칭은 대소문자를 무시한다.
--
-- 이 카탈로그는 monami TbStar 다. 캐시 DdwSql.SelectModels(TbStar) 와 SelectAttributes 와 같은 출처를 쓴다.
-- tb_modelinstances, tb_pocinstances 에 아래 열은 없다.
--   tag, s5_eqcode, construction_code, elementId_number
-- tb_modeltemplates 의 Subdivision_1_name, Subdivision_2_name 도 없다.
-- tb_pocinstances 의 tag, connectionTag 도 없다.
--
-- Subdivision1, Subdivision2 는 템플릿의 subdivision id 를 tb_division.division_value 로 읽는다.
-- tag 는 tb_attributes.attribute_nm = 'tag' 이다.
-- 건설코드는 tb_attributes.attribute_nm = 'Construction-code' 이다.
-- 좌표만 tb_modelinstances.posx, posy, posz 와 tb_pocinstances.posx, posy, posz 다.
--
-- 연결 서버 SELECT 를 파생 테이블로 감싼 뒤 src.x 처럼 별칭을 다시 읽지 않는다.
-- 그 별칭은 원격 테이블의 열로 묶여 Invalid column name 이 된다.
-- 임시 테이블은 CREATE TABLE 로 열을 먼저 선언한다. SELECT INTO 는 같은 세션에 남은 이전 열 이름을 컴파일 때 본다.
-- SSMS 에서 스크립트 전체를 실행한다. GO 가 배치를 나눈다.
--
-- pump: Subdivision1 = 4-pcs, Subdivision2 = pump
--   설비명에 _LL_ 이 있으면 로드락 펌프라 모수에서 뺀다. 대소문자는 무시한다.
-- 진공 POC: owner_type 768, POC template name 에 fore-line
--   POC 이름에 (LL) 또는 (PS) 가 있으면 그 POC 는 모수에서 뺀다.
-- middle foreline: Subdivision1 = 기타, Subdivision2 = middle foreline, 객체명에 foreline
--
-- 비교 좌표는 소수 2자리. X 차이와 Y 차이가 각각 1 이하면 같은 위치다.
-- 77629.95 와 77630 은 일치다. 높이 posz 는 비교하지 않는다.
-- 같은 위치의 피팅이 1개이고, 그 피팅에서 1 이내인 진공 POC 도 1개일 때만 직선 대응이다.
--
-- 연결 서버나 카탈로그가 다르면 remote_monami.monami 를 바꾼다.
-- live 는 version_sequence < 1000000000 이다.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#model') IS NOT NULL DROP TABLE #model;
IF OBJECT_ID('tempdb..#pump') IS NOT NULL DROP TABLE #pump;
IF OBJECT_ID('tempdb..#attr_named') IS NOT NULL DROP TABLE #attr_named;
IF OBJECT_ID('tempdb..#pump_attr') IS NOT NULL DROP TABLE #pump_attr;
IF OBJECT_ID('tempdb..#vac') IS NOT NULL DROP TABLE #vac;
IF OBJECT_ID('tempdb..#mid') IS NOT NULL DROP TABLE #mid;
IF OBJECT_ID('tempdb..#fit_for') IS NOT NULL DROP TABLE #fit_for;
IF OBJECT_ID('tempdb..#poc_near') IS NOT NULL DROP TABLE #poc_near;
IF OBJECT_ID('tempdb..#judged') IS NOT NULL DROP TABLE #judged;
GO

CREATE TABLE #model (
    instance_id uniqueidentifier NOT NULL,
    instance_nm nvarchar(512) NULL,
    sub1 nvarchar(256) NULL,
    sub2 nvarchar(256) NULL,
    pos_x float NULL,
    pos_y float NULL,
    pos_z float NULL,
    bucket_x int NULL,
    bucket_y int NULL
);

INSERT INTO #model (
    instance_id, instance_nm, sub1, sub2, pos_x, pos_y, pos_z, bucket_x, bucket_y
)
SELECT
    TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID),
    COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)),
    LOWER(CONVERT(nvarchar(256), div1.DIVISION_VALUE)),
    LOWER(CONVERT(nvarchar(256), div2.DIVISION_VALUE)),
    CONVERT(float, ROUND(mi.POSX, 2)),
    CONVERT(float, ROUND(mi.POSY, 2)),
    CONVERT(float, ROUND(mi.POSZ, 2)),
    CONVERT(int, ROUND(mi.POSX, 0)),
    CONVERT(int, ROUND(mi.POSY, 0))
FROM remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_modeltemplates AS mt WITH (NOLOCK)
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID
   AND mt.version_sequence < 1000000000
LEFT JOIN remote_monami.monami.dbo.tb_division AS div1 WITH (NOLOCK)
    ON div1.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_1_ID)
   AND div1.version_sequence < 1000000000
LEFT JOIN remote_monami.monami.dbo.tb_division AS div2 WITH (NOLOCK)
    ON div2.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_2_ID)
   AND div2.version_sequence < 1000000000
WHERE mi.version_sequence < 1000000000
  AND TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID) IS NOT NULL
  AND (
        (
            LOWER(CONVERT(nvarchar(256), div1.DIVISION_VALUE)) = N'4-pcs'
            AND LOWER(CONVERT(nvarchar(256), div2.DIVISION_VALUE)) = N'pump'
        )
        OR (
            LOWER(CONVERT(nvarchar(256), div1.DIVISION_VALUE)) = N'기타'
            AND LOWER(CONVERT(nvarchar(256), div2.DIVISION_VALUE)) = N'middle foreline'
            AND LOWER(ISNULL(COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)), N'')) LIKE N'%foreline%'
        )
      );

CREATE CLUSTERED INDEX ix_model ON #model(instance_id);

CREATE TABLE #pump (
    instance_id uniqueidentifier NOT NULL,
    instance_nm nvarchar(512) NULL,
    pos_x float NULL,
    pos_y float NULL,
    pos_z float NULL
);

INSERT INTO #pump (instance_id, instance_nm, pos_x, pos_y, pos_z)
SELECT instance_id, instance_nm, pos_x, pos_y, pos_z
FROM #model
WHERE sub1 = N'4-pcs'
  AND sub2 = N'pump'
  AND LOWER(ISNULL(instance_nm, N'')) NOT LIKE N'%\_ll\_%' ESCAPE N'\';

CREATE CLUSTERED INDEX ix_pump ON #pump(instance_id);

CREATE TABLE #attr_named (
    owner_id uniqueidentifier NULL,
    attribute_nm nvarchar(256) NULL,
    attribute_val nvarchar(512) NULL
);

INSERT INTO #attr_named (owner_id, attribute_nm, attribute_val)
SELECT
    TRY_CONVERT(uniqueidentifier, a.OWNER_ID),
    CONVERT(nvarchar(256), a.ATTRIBUTE_NM),
    LTRIM(RTRIM(CONVERT(nvarchar(512), a.ATTRIBUTE_VAL)))
FROM remote_monami.monami.dbo.tb_attributes AS a WITH (NOLOCK)
WHERE a.version_sequence < 1000000000
  AND a.ATTRIBUTE_NM IN (N'tag', N'Construction-code')
  AND a.ATTRIBUTE_VAL IS NOT NULL
  AND LTRIM(RTRIM(CONVERT(nvarchar(512), a.ATTRIBUTE_VAL))) <> N'';

CREATE TABLE #pump_attr (
    owner_id uniqueidentifier NOT NULL,
    tag nvarchar(512) NULL,
    construction_code nvarchar(512) NULL
);

INSERT INTO #pump_attr (owner_id, tag, construction_code)
SELECT
    a.owner_id,
    MAX(CASE WHEN a.attribute_nm = N'tag' THEN a.attribute_val END),
    MAX(CASE WHEN a.attribute_nm = N'Construction-code' THEN a.attribute_val END)
FROM #attr_named AS a
INNER JOIN #pump AS p
    ON p.instance_id = a.owner_id
WHERE a.owner_id IS NOT NULL
GROUP BY a.owner_id;

DROP TABLE #attr_named;

CREATE CLUSTERED INDEX ix_pump_attr ON #pump_attr(owner_id);

CREATE TABLE #vac (
    pump_id uniqueidentifier NOT NULL,
    pump_tag nvarchar(512) NULL,
    pump_name nvarchar(512) NULL,
    pump_construction_code nvarchar(512) NULL,
    pump_x float NULL,
    pump_y float NULL,
    pump_z float NULL,
    poc_id uniqueidentifier NULL,
    poc_name nvarchar(512) NULL,
    pos_x float NULL,
    pos_y float NULL,
    pos_z float NULL,
    bucket_x int NULL,
    bucket_y int NULL
);

INSERT INTO #vac (
    pump_id, pump_tag, pump_name, pump_construction_code,
    pump_x, pump_y, pump_z,
    poc_id, poc_name, pos_x, pos_y, pos_z, bucket_x, bucket_y
)
SELECT
    pump.instance_id,
    attr.tag,
    pump.instance_nm,
    attr.construction_code,
    pump.pos_x,
    pump.pos_y,
    pump.pos_z,
    TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID),
    CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM),
    CONVERT(float, ROUND(p.POSX, 2)),
    CONVERT(float, ROUND(p.POSY, 2)),
    CONVERT(float, ROUND(p.POSZ, 2)),
    CONVERT(int, ROUND(p.POSX, 0)),
    CONVERT(int, ROUND(p.POSY, 0))
FROM #pump AS pump
LEFT JOIN #pump_attr AS attr
    ON attr.owner_id = pump.instance_id
INNER JOIN remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
    ON TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID) = pump.instance_id
   AND p.version_sequence < 1000000000
   AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768
INNER JOIN remote_monami.monami.dbo.tb_poctemplates AS pt WITH (NOLOCK)
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID
   AND pt.version_sequence < 1000000000
WHERE LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) LIKE N'%fore-line%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ll)%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ps)%';

CREATE CLUSTERED INDEX ix_vac_xy ON #vac(bucket_x, bucket_y);

CREATE TABLE #mid (
    instance_id uniqueidentifier NOT NULL,
    instance_nm nvarchar(512) NULL,
    pos_x float NOT NULL,
    pos_y float NOT NULL,
    pos_z float NULL,
    bucket_x int NULL,
    bucket_y int NULL
);

INSERT INTO #mid (instance_id, instance_nm, pos_x, pos_y, pos_z, bucket_x, bucket_y)
SELECT instance_id, instance_nm, pos_x, pos_y, pos_z, bucket_x, bucket_y
FROM #model
WHERE sub1 = N'기타'
  AND sub2 = N'middle foreline'
  AND LOWER(ISNULL(instance_nm, N'')) LIKE N'%foreline%'
  AND pos_x IS NOT NULL
  AND pos_y IS NOT NULL;

CREATE CLUSTERED INDEX ix_mid_xy ON #mid(bucket_x, bucket_y);

CREATE TABLE #fit_for (
    poc_id uniqueidentifier NULL,
    fit_count int NOT NULL
);

INSERT INTO #fit_for (poc_id, fit_count)
SELECT
    v.poc_id,
    COUNT(*)
FROM #vac AS v
INNER JOIN #mid AS m
    ON m.bucket_x BETWEEN v.bucket_x - 1 AND v.bucket_x + 1
   AND m.bucket_y BETWEEN v.bucket_y - 1 AND v.bucket_y + 1
   AND ABS(m.pos_x - v.pos_x) <= 1
   AND ABS(m.pos_y - v.pos_y) <= 1
WHERE v.pos_x IS NOT NULL
  AND v.pos_y IS NOT NULL
GROUP BY v.poc_id;

CREATE CLUSTERED INDEX ix_fit_for ON #fit_for(poc_id);

CREATE TABLE #poc_near (
    poc_id uniqueidentifier NULL,
    poc_count int NOT NULL
);

INSERT INTO #poc_near (poc_id, poc_count)
SELECT
    v.poc_id,
    COUNT(DISTINCT o.poc_id)
FROM #vac AS v
INNER JOIN #mid AS m
    ON m.bucket_x BETWEEN v.bucket_x - 1 AND v.bucket_x + 1
   AND m.bucket_y BETWEEN v.bucket_y - 1 AND v.bucket_y + 1
   AND ABS(m.pos_x - v.pos_x) <= 1
   AND ABS(m.pos_y - v.pos_y) <= 1
INNER JOIN #vac AS o
    ON o.bucket_x BETWEEN m.bucket_x - 1 AND m.bucket_x + 1
   AND o.bucket_y BETWEEN m.bucket_y - 1 AND m.bucket_y + 1
   AND o.pos_x IS NOT NULL
   AND o.pos_y IS NOT NULL
   AND ABS(o.pos_x - m.pos_x) <= 1
   AND ABS(o.pos_y - m.pos_y) <= 1
WHERE v.pos_x IS NOT NULL
  AND v.pos_y IS NOT NULL
GROUP BY v.poc_id;

CREATE CLUSTERED INDEX ix_poc_near ON #poc_near(poc_id);

CREATE TABLE #judged (
    pump_id uniqueidentifier NOT NULL,
    pump_tag nvarchar(512) NULL,
    pump_name nvarchar(512) NULL,
    pump_construction_code nvarchar(512) NULL,
    pump_x float NULL,
    pump_y float NULL,
    pump_z float NULL,
    poc_id uniqueidentifier NULL,
    poc_name nvarchar(512) NULL,
    poc_x float NULL,
    poc_y float NULL,
    poc_z float NULL,
    fitting_count_at_xy int NOT NULL,
    poc_count_at_xy int NOT NULL,
    align_result nvarchar(50) NOT NULL
);

INSERT INTO #judged (
    pump_id, pump_tag, pump_name, pump_construction_code,
    pump_x, pump_y, pump_z,
    poc_id, poc_name, poc_x, poc_y, poc_z,
    fitting_count_at_xy, poc_count_at_xy, align_result
)
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
    v.pos_x,
    v.pos_y,
    v.pos_z,
    ISNULL(f.fit_count, 0),
    ISNULL(n.poc_count, 0),
    CASE
        WHEN v.pos_x IS NULL OR v.pos_y IS NULL THEN N'좌표없음'
        WHEN ISNULL(f.fit_count, 0) = 0 THEN N'middle foreline 없음'
        WHEN f.fit_count > 1 THEN N'middle foreline 여러개'
        WHEN ISNULL(n.poc_count, 0) > 1 THEN N'진공POC 여러개'
        ELSE N'일치'
    END
FROM #vac AS v
LEFT JOIN #fit_for AS f
    ON f.poc_id = v.poc_id
LEFT JOIN #poc_near AS n
    ON n.poc_id = v.poc_id;

SELECT
    align_result,
    pump_name,
    pump_tag,
    pump_construction_code,
    pump_x,
    pump_y,
    pump_z,
    poc_id,
    poc_name,
    poc_x,
    poc_y,
    poc_z,
    fitting_count_at_xy,
    poc_count_at_xy
FROM #judged
WHERE align_result <> N'일치'
ORDER BY pump_tag, pump_name, poc_name, poc_x, poc_y;

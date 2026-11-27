-- pump 설비 수, fore-line POC 수, middle foreline 수.
-- 모수는 docs/MiddleForelineAlign.sql 과 같다. X, Y 일치는 보지 않는다.
--
-- 이 카탈로그는 monami TbStar 다.
-- Subdivision 은 tb_division.division_value 다.
-- 좌표 조인은 하지 않는다. tag 와 Construction-code 도 읽지 않는다.
--
-- pump: Subdivision1 = 4-pcs, Subdivision2 = pump
--   설비명에 _LL_ 이 있으면 로드락 펌프라 모수에서 뺀다.
-- 진공 POC: owner_type 768, POC template name 에 fore-line
--   POC 이름에 (LL) 또는 (PS) 가 있으면 그 POC 는 모수에서 뺀다.
--   위 pump 의 POC 만 센다.
-- middle foreline: Subdivision1 = 기타, Subdivision2 = middle foreline, 객체명에 foreline
--   좌표가 없어도 수량에 넣는다.
-- live 는 version_sequence < 1000000000 이다.
-- 연결 서버나 카탈로그가 다르면 remote_monami.monami 를 바꾼다.
--
-- 연결 서버 SELECT 를 파생 테이블로 감싸 별칭을 다시 읽지 않는다.
-- 임시 테이블은 CREATE TABLE 로 열을 먼저 선언한다.
-- SSMS 에서 스크립트 전체를 실행한다. GO 가 배치를 나눈다.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#model') IS NOT NULL DROP TABLE #model;
IF OBJECT_ID('tempdb..#pump') IS NOT NULL DROP TABLE #pump;
IF OBJECT_ID('tempdb..#vac') IS NOT NULL DROP TABLE #vac;
IF OBJECT_ID('tempdb..#mid') IS NOT NULL DROP TABLE #mid;
GO

CREATE TABLE #model (
    instance_id uniqueidentifier NOT NULL,
    instance_nm nvarchar(512) NULL,
    sub1 nvarchar(256) NULL,
    sub2 nvarchar(256) NULL
);

INSERT INTO #model (instance_id, instance_nm, sub1, sub2)
SELECT
    TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID),
    COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)),
    LOWER(CONVERT(nvarchar(256), div1.DIVISION_VALUE)),
    LOWER(CONVERT(nvarchar(256), div2.DIVISION_VALUE))
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
    instance_id uniqueidentifier NOT NULL
);

INSERT INTO #pump (instance_id)
SELECT instance_id
FROM #model
WHERE sub1 = N'4-pcs'
  AND sub2 = N'pump'
  AND LOWER(ISNULL(instance_nm, N'')) NOT LIKE N'%\_ll\_%' ESCAPE N'\';

CREATE CLUSTERED INDEX ix_pump ON #pump(instance_id);

CREATE TABLE #vac (
    poc_id uniqueidentifier NOT NULL
);

INSERT INTO #vac (poc_id)
SELECT TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID)
FROM #pump AS pump
INNER JOIN remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
    ON TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID) = pump.instance_id
   AND p.version_sequence < 1000000000
   AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768
INNER JOIN remote_monami.monami.dbo.tb_poctemplates AS pt WITH (NOLOCK)
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID
   AND pt.version_sequence < 1000000000
WHERE LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) LIKE N'%fore-line%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ll)%'
  AND LOWER(CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM)) NOT LIKE N'%(ps)%'
  AND TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID) IS NOT NULL;

CREATE CLUSTERED INDEX ix_vac ON #vac(poc_id);

CREATE TABLE #mid (
    instance_id uniqueidentifier NOT NULL
);

INSERT INTO #mid (instance_id)
SELECT instance_id
FROM #model
WHERE sub1 = N'기타'
  AND sub2 = N'middle foreline'
  AND LOWER(ISNULL(instance_nm, N'')) LIKE N'%foreline%';

CREATE CLUSTERED INDEX ix_mid ON #mid(instance_id);

SELECT
    (SELECT COUNT(*) FROM #pump) AS pump_count,
    (SELECT COUNT(*) FROM #vac) AS poc_count,
    (SELECT COUNT(*) FROM #mid) AS middle_foreline_count;

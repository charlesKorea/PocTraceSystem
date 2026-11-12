-- T01 설비 Tag 에서 배관 길이까지 한 단계씩 따라가는 쿼리. 빠름. 설계: docs/DDW_Trace_Flow_KO.md
-- 담당자가 SVDI 에서 손으로 따라간 순서(2026-11-12)를 그대로 쿼리로 옮겼다. 모든 테이블에 version_sequence < 1000000000.
-- 한 번에 한 단계씩 돌린다. 단계 출력의 *_hex 전체를 다음 단계의 DECLARE 에 붙여 넣는다.
-- 회신에는 Tag 원문과 hex 전체를 쓰지 않는다. 회신은 앞 10자(0x 포함)와 숫자 열만 보낸다.
-- id 열이 binary 인지 uniqueidentifier 인지에 상관없이 0x 리터럴로 비교한다.
-- attribute_val 이 ntext 라서 = 비교가 에러(402)면 에러 메시지만 보내고 LIKE 로 바꾸지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Tag      nvarchar(200)  = N'<TAG>';
DECLARE @Eq       varbinary(16)  = 0x00000000000000000000000000000000;  -- 1단계 owner_id (설비 인스턴스)
DECLARE @Tpl      varbinary(16)  = 0x00000000000000000000000000000000;  -- 2단계 model_template_id
DECLARE @StartPoc varbinary(16)  = 0x00000000000000000000000000000000;  -- 5단계 설비 POC instance_id
DECLARE @Next     varbinary(16)  = 0x00000000000000000000000000000000;  -- 5단계 connected_poc_id (건너편 POC)
DECLARE @NextOwn  varbinary(16)  = 0x00000000000000000000000000000000;  -- 6단계 owner_instance_id (건너편 소유 객체)
DECLARE @Pipe     varbinary(16)  = 0x00000000000000000000000000000000;  -- 8단계 poc_template_id 가 0 인 POC 의 owner_instance_id

-- 1. Tag 속성으로 owner 찾기. Tag 는 속성 테이블의 attribute_nm = 'tag' 에 있다.
SELECT TOP 20
    CONVERT(varchar(34), CONVERT(varbinary(16), a.owner_id), 1) AS owner_hex,
    a.owner_type,
    a.version_sequence
FROM remote_monami.monami.dbo.tb_attributes AS a WITH (NOLOCK)
WHERE a.attribute_nm = N'tag'
  AND a.attribute_val = @Tag
  AND a.version_sequence < 1000000000;

-- 2. 설비 여부. 배치 객체 정보. model_type 50594049 를 설비로 본다.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), mi.instance_id), 1) AS instance_hex,
    mi.model_type,
    mi.composite_yn,
    CONVERT(varchar(34), CONVERT(varbinary(16), mi.model_template_id), 1) AS template_hex,
    mi.version_sequence
FROM remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
WHERE mi.instance_id = @Eq
  AND mi.version_sequence < 1000000000;

-- 3. 라이브러리 모델. display_nm 이 설비 이름, family_key = equipment 가 힌트, bim_yn = 0 이 2차 설계 객체.
SELECT
    LEFT(mt.display_nm, 12) AS display12,
    LEN(mt.display_nm) AS display_len,
    mt.family_key,
    mt.bim_yn,
    mt.version_sequence
FROM remote_monami.monami.dbo.tb_modeltemplates AS mt WITH (NOLOCK)
WHERE mt.model_template_id = @Tpl
  AND mt.version_sequence < 1000000000;

-- 4. 라이브러리 POC. equipment_yn = 1 이 설비 POC, memo 는 비고.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), pt.poc_template_id), 1) AS poc_template_hex,
    LEFT(pt.poc_template_nm, 16) AS poc_nm16,
    CONVERT(varchar(34), CONVERT(varbinary(16), pt.utility_id), 1) AS utility_hex,
    pt.equipment_yn,
    CASE WHEN pt.memo IS NULL OR LTRIM(RTRIM(pt.memo)) = '' THEN 0 ELSE LEN(pt.memo) END AS memo_len
FROM remote_monami.monami.dbo.tb_poctemplates AS pt WITH (NOLOCK)
WHERE pt.model_template_id = @Tpl
  AND pt.version_sequence < 1000000000;

-- 5. 배치된 설비 POC. 설비 id(owner_instance_id)를 반드시 걸어야 그 설비의 POC 만 나온다.
--    connected_poc_id 가 S5D 의 outlet 이다.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), p.instance_id), 1) AS poc_hex,
    p.owner_type,
    p.connection_order,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.poc_template_id), 1) AS poc_template_hex,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.utility_id), 1) AS utility_hex,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.connected_poc_id), 1) AS connected_hex,
    CASE WHEN p.connected_poc_id IS NULL THEN 'null'
         WHEN p.connected_poc_id = 0x00000000000000000000000000000000 THEN 'zero'
         ELSE 'set' END AS connected
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
WHERE p.owner_instance_id = @Eq
  AND p.version_sequence < 1000000000
ORDER BY p.connection_order;

-- 6. 건너편 POC. connected_poc_id 가 지나온 POC(@StartPoc)를 가리키면 되돌아가지 않는 규칙을 확인한다.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), p.instance_id), 1) AS poc_hex,
    p.owner_type,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.poc_template_id), 1) AS poc_template_hex,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.owner_instance_id), 1) AS owner_hex,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.connected_poc_id), 1) AS connected_hex,
    CASE WHEN p.connected_poc_id = @StartPoc THEN 1 ELSE 0 END AS points_back
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
WHERE p.instance_id = @Next
  AND p.version_sequence < 1000000000;

-- 7. 건너편 소유 객체. model_type 51380485 가 connector_pipe. 그 객체의 POC 전부와 아직 지나지 않은 POC.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), mi.instance_id), 1) AS instance_hex,
    mi.model_type,
    mi.composite_yn,
    CONVERT(varchar(34), CONVERT(varbinary(16), mi.model_template_id), 1) AS template_hex
FROM remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
WHERE mi.instance_id = @NextOwn
  AND mi.version_sequence < 1000000000;

SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), p.instance_id), 1) AS poc_hex,
    p.connection_order,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.poc_template_id), 1) AS poc_template_hex,
    CONVERT(varchar(34), CONVERT(varbinary(16), p.connected_poc_id), 1) AS connected_hex,
    CASE WHEN p.instance_id = @Next THEN 1 ELSE 0 END AS arrived_here
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
WHERE p.owner_instance_id = @NextOwn
  AND p.version_sequence < 1000000000
ORDER BY p.connection_order;

-- 8. poc_template_id 가 0 인 POC 를 만나면 owner_instance_id 로 배관 테이블을 본다.
--    length2 가 배관 길이(mm, 변환 불필요)라는 담당자 설명. length1 과 같이 본다. 관경 열 이름은 03 결과를 보고 더한다.
SELECT
    CONVERT(varchar(34), CONVERT(varbinary(16), pi.instance_id), 1) AS pipe_hex,
    pi.pipe_type,
    pi.length1,
    pi.length2,
    CONVERT(varchar(34), CONVERT(varbinary(16), pi.utility_id), 1) AS utility_hex,
    pi.version_sequence
FROM remote_monami.monami.dbo.tb_pipeinstances AS pi WITH (NOLOCK)
WHERE pi.instance_id = @Pipe
  AND pi.version_sequence < 1000000000;

-- 9. 이 과정을 건너편 POC 마다 되풀이한다. 지나온 POC 는 건너뛰고, 다른 설비, 노즐, 캡에서는 멈춘다(종료 조건은 설계 문서 질문 참고).

-- 10 행의 모양 샘플. 빠름. TOP 이라 분포가 아니다.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- 모든 tb_* 에 version_sequence 가 있다(2026-11-11 담당자 확인).
-- GUID 전문, Tag 전문, 사람 이름은 내지 않는다.
-- 결과 그리드를 모두 복사한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT TOP 8
    'model' AS src,
    model_type,
    composite_yn,
    CASE
        WHEN parent_instance_id IS NULL
          OR parent_instance_id = '00000000-0000-0000-0000-000000000000' THEN 1
        ELSE 0
    END AS parent_null,
    CASE WHEN instance_nm IS NULL THEN 0 ELSE LEN(instance_nm) END AS name_len,
    LEFT(ISNULL(instance_nm, ''), 12) AS name12,
    ROUND(posx, 1) AS posx,
    ROUND(posy, 1) AS posy,
    ROUND(posz, 1) AS posz,
    version_sequence
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK);

SELECT TOP 8
    'template' AS src,
    model_type,
    bim_yn,
    CASE WHEN division_id IS NULL OR LTRIM(RTRIM(division_id)) = '' THEN 0 ELSE 1 END AS has_division,
    CASE WHEN subdivision_1_id IS NULL OR LTRIM(RTRIM(subdivision_1_id)) = '' THEN 0 ELSE 1 END AS has_sub1,
    CASE WHEN subdivision_2_id IS NULL OR LTRIM(RTRIM(subdivision_2_id)) = '' THEN 0 ELSE 1 END AS has_sub2,
    CASE WHEN family_key IS NULL THEN 0 ELSE LEN(family_key) END AS family_len,
    LEFT(ISNULL(display_nm, ''), 16) AS display16,
    LEFT(ISNULL(model_template_nm, ''), 16) AS template16
FROM remote_monami.monami.dbo.tb_modeltemplates WITH (NOLOCK);

SELECT TOP 8
    'poc' AS src,
    owner_type,
    connection_order,
    poc_shape,
    ROUND(diameter, 3) AS diameter,
    ROUND(width, 3) AS width,
    CASE WHEN poc_template_id IS NULL THEN 0 ELSE 1 END AS has_template,
    CASE WHEN utility_id IS NULL OR utility_id = '00000000-0000-0000-0000-000000000000' THEN 0 ELSE 1 END AS has_utility,
    CASE
        WHEN connected_poc_id IS NULL THEN 'null'
        WHEN connected_poc_id = '00000000-0000-0000-0000-000000000000' THEN 'zero'
        ELSE 'set'
    END AS connected,
    version_sequence
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK);

SELECT TOP 8
    'pipe' AS src,
    pipe_type,
    CASE WHEN length1 IS NULL THEN NULL ELSE ROUND(length1, 3) END AS length1,
    CASE WHEN diameter1 IS NULL THEN NULL ELSE ROUND(diameter1, 3) END AS diameter1,
    CASE WHEN utility_id IS NULL OR utility_id = '00000000-0000-0000-0000-000000000000' THEN 0 ELSE 1 END AS has_utility,
    ROUND(posz, 1) AS posz,
    version_sequence
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK);

SELECT TOP 8
    'poctemplate' AS src,
    takeoff_yn,
    equipment_yn,
    CASE WHEN memo IS NULL OR LTRIM(RTRIM(memo)) = '' THEN 0 ELSE LEN(memo) END AS memo_len,
    LEFT(ISNULL(poc_template_nm, ''), 16) AS name16
FROM remote_monami.monami.dbo.tb_poctemplates WITH (NOLOCK);

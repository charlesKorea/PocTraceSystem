-- x2_m 캐시 조회. 배치는 GO 로 나뉜다. SSMS에서 한 배치씩 실행한다.
-- 카탈로그는 [remote_monami].[monami] 이다. 캐시 폴더 키 x2_m 과 다르다.
-- 01, 02는 열 이름 확인이다. 03은 배관 version_sequence 와 tb_versions.version_number 가 맞는지 본다.
-- 04부터 08은 캐시와 같은 SELECT를 TOP 20으로 감싼 것이다. 열 오류는 여기서 난다.
-- 09부터 13은 캐시 버튼이 제한 없이 실행하는 SQL이다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
GO

-- 01 versions 열
SELECT TOP 20 v.VERSION_NUMBER, v.USER_ID, v.LAST_UPDATE FROM [remote_monami].[monami].dbo.TB_VERSIONS v
GO

-- 02 users 열
SELECT TOP 20 nu.USER_ID, nu.LOGIN_ID, nu.USER_NAME, nu.COMPANY_NAME FROM [remote_monami].[monami].dbo.TB_NEW_USERS nu
GO

-- 03 pipe version match
SELECT TOP 20
    pi.VERSION_SEQUENCE,
    v.VERSION_NUMBER,
    v.USER_ID,
    v.LAST_UPDATE,
    nu.LOGIN_ID,
    nu.USER_NAME,
    nu.COMPANY_NAME
FROM [remote_monami].[monami].dbo.TB_PIPEINSTANCES pi
LEFT JOIN [remote_monami].[monami].dbo.TB_VERSIONS v
    ON v.VERSION_NUMBER = pi.VERSION_SEQUENCE
LEFT JOIN [remote_monami].[monami].dbo.TB_NEW_USERS nu
    ON nu.USER_ID = v.USER_ID
WHERE pi.version_sequence < 1000000000
GO

-- 04 cache model POC TOP 20
SELECT TOP 20 * FROM (
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerInstanceId,
    TRY_CONVERT(uniqueidentifier, p.CONNECTED_POC_ID) AS ConnectedPocId,
    TRY_CAST(p.OWNER_TYPE AS int) AS OwnerType,
    ISNULL(TRY_CAST(p.CONNECTION_ORDER AS int), 0) AS ConnectionOrder,
    ISNULL(TRY_CAST(p.POC_SHAPE AS int), 0) AS PocShape,
    TRY_CAST(p.DIAMETER AS float) AS Diameter,
    TRY_CAST(p.WIDTH AS float) AS Width,
    TRY_CAST(psz.DIAMETER AS float) AS TemplateDiameter,
    TRY_CAST(psz.WIDTH AS float) AS TemplateWidth,
    CONVERT(nvarchar(256), iu.UTILITY_NM) AS InstanceUtilityName,
    CONVERT(nvarchar(256), tu.UTILITY_NM) AS TemplateUtilityName,
    CONVERT(nvarchar(256), ig.UTILITY_GROUP_NM) AS InstanceUtilityGroupName,
    CONVERT(nvarchar(256), tg.UTILITY_GROUP_NM) AS TemplateUtilityGroupName,
    CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM) AS PocTemplateName,
    CONVERT(nvarchar(512), pt.MEMO) AS Memo,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(NULL AS nvarchar(512)) AS ConnectionTag,
    CAST(0 AS int) AS OperationType,
    TRY_CONVERT(uniqueidentifier, tgt.INSTANCE_ID) AS TargetInstanceId,
    TRY_CAST(tgt.OWNER_TYPE AS int) AS TargetOwnerType,
    TRY_CONVERT(uniqueidentifier, tgt.OWNER_INSTANCE_ID) AS TargetOwnerInstanceId
FROM [remote_monami].[monami].dbo.TB_POCINSTANCES p
LEFT JOIN [remote_monami].[monami].dbo.TB_POCTEMPLATES pt
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID AND pt.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_PIPESIZE psz
    ON psz.PIPESIZE_ID = COALESCE(p.PIPESIZE_ID, pt.PIPESIZE_ID) AND psz.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES iu
    ON iu.UTILITY_ID = p.UTILITY_ID AND iu.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES tu
    ON tu.UTILITY_ID = pt.UTILITY_ID AND tu.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITY_GROUPS ig
    ON ig.UTILITY_GROUP_ID = iu.UTILITY_GROUP_ID AND ig.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITY_GROUPS tg
    ON tg.UTILITY_GROUP_ID = tu.UTILITY_GROUP_ID AND tg.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_POCINSTANCES tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.version_sequence < 1000000000
WHERE p.version_sequence < 1000000000 AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768) AS cache_rows
GO

-- 05 cache pipe POC TOP 20
SELECT TOP 20 * FROM (
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerInstanceId,
    TRY_CONVERT(uniqueidentifier, p.CONNECTED_POC_ID) AS ConnectedPocId,
    TRY_CAST(p.OWNER_TYPE AS int) AS OwnerType,
    ISNULL(TRY_CAST(p.CONNECTION_ORDER AS int), 0) AS ConnectionOrder,
    CAST(0 AS int) AS OperationType,
    TRY_CONVERT(uniqueidentifier, tgt.INSTANCE_ID) AS TargetInstanceId,
    TRY_CAST(tgt.OWNER_TYPE AS int) AS TargetOwnerType,
    TRY_CONVERT(uniqueidentifier, tgt.OWNER_INSTANCE_ID) AS TargetOwnerInstanceId
FROM [remote_monami].[monami].dbo.TB_POCINSTANCES p
LEFT JOIN [remote_monami].[monami].dbo.TB_POCINSTANCES tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.version_sequence < 1000000000
WHERE p.version_sequence < 1000000000
  AND TRY_CONVERT(uniqueidentifier, p.POC_TEMPLATE_ID) = CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS cache_rows
GO

-- 06 cache pipe TOP 20
SELECT TOP 20 * FROM (
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, pi.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CAST(pi.PIPE_TYPE AS int), 0) AS PipeType,
    CAST(CASE WHEN pi.LENGTH2 IS NULL OR pi.LENGTH2 <= 0 THEN 0 ELSE ROUND(pi.LENGTH2, 1) END AS float) AS Length,
    CONVERT(nvarchar(256), u.UTILITY_NM) AS UtilityName,
    TRY_CAST(v.LAST_UPDATE AS datetime2) AS OpTime,
    CONVERT(nvarchar(256), nu.USER_NAME) AS UserName,
    CONVERT(nvarchar(256), nu.COMPANY_NAME) AS CompanyName,
    CONVERT(nvarchar(256), nu.LOGIN_ID) AS LoginId,
    TRY_CAST(pi.DIAMETER1 AS float) AS PipeSizeMm,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_PIPEINSTANCES pi
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES u
    ON u.UTILITY_ID = pi.UTILITY_ID AND u.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_VERSIONS v
    ON v.VERSION_NUMBER = pi.VERSION_SEQUENCE
LEFT JOIN [remote_monami].[monami].dbo.TB_NEW_USERS nu
    ON nu.USER_ID = v.USER_ID
WHERE pi.version_sequence < 1000000000) AS cache_rows
GO

-- 07 cache model TOP 20
SELECT TOP 20 * FROM (
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)) AS Name,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(NULL AS nvarchar(512)) AS ConstructionCode,
    CAST(NULL AS nvarchar(512)) AS BimId,
    TRY_CAST(mi.MODEL_TYPE AS int) AS ModelType,
    TRY_CAST(mi.COMPOSITE_YN AS int) AS CompositeYn,
    TRY_CONVERT(uniqueidentifier, mi.PARENT_INSTANCE_ID) AS ParentInstanceId,
    CONVERT(nvarchar(256), mt.FAMILY_KEY) AS FamilyKey,
    TRY_CAST(mt.BIM_YN AS int) AS BimYn,
    TRY_CAST(div0.DIVISION_TYPE AS int) AS DivisionType,
    CONVERT(nvarchar(256), div0.DIVISION_VALUE) AS DivisionName,
    CONVERT(nvarchar(256), sub1.DIVISION_VALUE) AS Sub1Name,
    CONVERT(nvarchar(256), sub2.DIVISION_VALUE) AS Sub2Name,
    TRY_CAST(sub1.DIVISION_TYPE AS int) AS Sub1DivisionType,
    CAST(ROUND(mi.POSX, 1) AS float) AS PosX,
    CAST(ROUND(mi.POSY, 1) AS float) AS PosY,
    CAST(ROUND(mi.POSZ, 1) AS float) AS PosZ,
    ISNULL(TRY_CAST(ROUND(mi.POSZ, 0) AS bigint), 0) AS Pz,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_MODELINSTANCES mi
LEFT JOIN [remote_monami].[monami].dbo.TB_MODELTEMPLATES mt
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID AND mt.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION div0
    ON div0.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.DIVISION_ID) AND div0.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION sub1
    ON sub1.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_1_ID) AND sub1.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION sub2
    ON sub2.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_2_ID) AND sub2.version_sequence < 1000000000
WHERE mi.version_sequence < 1000000000) AS cache_rows
GO

-- 08 cache attribute TOP 20
SELECT TOP 20 * FROM (
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, a.OWNER_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerId,
    CONVERT(nvarchar(256), a.ATTRIBUTE_NM) AS Name,
    CONVERT(nvarchar(4000), a.ATTRIBUTE_VAL) AS Value,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_ATTRIBUTES a
WHERE a.version_sequence < 1000000000
  AND a.ATTRIBUTE_NM IN (N'Element ID_Number', N'__MODULE_INFO', N'tag', N'Construction-code', N'ConnectionTag', N'S5_LATERAL_NUMBER', N'HookupNumber')) AS cache_rows
GO

-- 09 cache model POC
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerInstanceId,
    TRY_CONVERT(uniqueidentifier, p.CONNECTED_POC_ID) AS ConnectedPocId,
    TRY_CAST(p.OWNER_TYPE AS int) AS OwnerType,
    ISNULL(TRY_CAST(p.CONNECTION_ORDER AS int), 0) AS ConnectionOrder,
    ISNULL(TRY_CAST(p.POC_SHAPE AS int), 0) AS PocShape,
    TRY_CAST(p.DIAMETER AS float) AS Diameter,
    TRY_CAST(p.WIDTH AS float) AS Width,
    TRY_CAST(psz.DIAMETER AS float) AS TemplateDiameter,
    TRY_CAST(psz.WIDTH AS float) AS TemplateWidth,
    CONVERT(nvarchar(256), iu.UTILITY_NM) AS InstanceUtilityName,
    CONVERT(nvarchar(256), tu.UTILITY_NM) AS TemplateUtilityName,
    CONVERT(nvarchar(256), ig.UTILITY_GROUP_NM) AS InstanceUtilityGroupName,
    CONVERT(nvarchar(256), tg.UTILITY_GROUP_NM) AS TemplateUtilityGroupName,
    CONVERT(nvarchar(512), pt.POC_TEMPLATE_NM) AS PocTemplateName,
    CONVERT(nvarchar(512), pt.MEMO) AS Memo,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(NULL AS nvarchar(512)) AS ConnectionTag,
    CAST(0 AS int) AS OperationType,
    TRY_CONVERT(uniqueidentifier, tgt.INSTANCE_ID) AS TargetInstanceId,
    TRY_CAST(tgt.OWNER_TYPE AS int) AS TargetOwnerType,
    TRY_CONVERT(uniqueidentifier, tgt.OWNER_INSTANCE_ID) AS TargetOwnerInstanceId
FROM [remote_monami].[monami].dbo.TB_POCINSTANCES p
LEFT JOIN [remote_monami].[monami].dbo.TB_POCTEMPLATES pt
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID AND pt.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_PIPESIZE psz
    ON psz.PIPESIZE_ID = COALESCE(p.PIPESIZE_ID, pt.PIPESIZE_ID) AND psz.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES iu
    ON iu.UTILITY_ID = p.UTILITY_ID AND iu.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES tu
    ON tu.UTILITY_ID = pt.UTILITY_ID AND tu.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITY_GROUPS ig
    ON ig.UTILITY_GROUP_ID = iu.UTILITY_GROUP_ID AND ig.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITY_GROUPS tg
    ON tg.UTILITY_GROUP_ID = tu.UTILITY_GROUP_ID AND tg.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_POCINSTANCES tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.version_sequence < 1000000000
WHERE p.version_sequence < 1000000000 AND ISNULL(TRY_CAST(p.OWNER_TYPE AS int), 0) = 768
GO

-- 10 cache pipe POC
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, p.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CONVERT(uniqueidentifier, p.OWNER_INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerInstanceId,
    TRY_CONVERT(uniqueidentifier, p.CONNECTED_POC_ID) AS ConnectedPocId,
    TRY_CAST(p.OWNER_TYPE AS int) AS OwnerType,
    ISNULL(TRY_CAST(p.CONNECTION_ORDER AS int), 0) AS ConnectionOrder,
    CAST(0 AS int) AS OperationType,
    TRY_CONVERT(uniqueidentifier, tgt.INSTANCE_ID) AS TargetInstanceId,
    TRY_CAST(tgt.OWNER_TYPE AS int) AS TargetOwnerType,
    TRY_CONVERT(uniqueidentifier, tgt.OWNER_INSTANCE_ID) AS TargetOwnerInstanceId
FROM [remote_monami].[monami].dbo.TB_POCINSTANCES p
LEFT JOIN [remote_monami].[monami].dbo.TB_POCINSTANCES tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.version_sequence < 1000000000
WHERE p.version_sequence < 1000000000
  AND TRY_CONVERT(uniqueidentifier, p.POC_TEMPLATE_ID) = CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')
GO

-- 11 cache pipe
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, pi.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    ISNULL(TRY_CAST(pi.PIPE_TYPE AS int), 0) AS PipeType,
    CAST(CASE WHEN pi.LENGTH2 IS NULL OR pi.LENGTH2 <= 0 THEN 0 ELSE ROUND(pi.LENGTH2, 1) END AS float) AS Length,
    CONVERT(nvarchar(256), u.UTILITY_NM) AS UtilityName,
    TRY_CAST(v.LAST_UPDATE AS datetime2) AS OpTime,
    CONVERT(nvarchar(256), nu.USER_NAME) AS UserName,
    CONVERT(nvarchar(256), nu.COMPANY_NAME) AS CompanyName,
    CONVERT(nvarchar(256), nu.LOGIN_ID) AS LoginId,
    TRY_CAST(pi.DIAMETER1 AS float) AS PipeSizeMm,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_PIPEINSTANCES pi
LEFT JOIN [remote_monami].[monami].dbo.TB_UTILITIES u
    ON u.UTILITY_ID = pi.UTILITY_ID AND u.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_VERSIONS v
    ON v.VERSION_NUMBER = pi.VERSION_SEQUENCE
LEFT JOIN [remote_monami].[monami].dbo.TB_NEW_USERS nu
    ON nu.USER_ID = v.USER_ID
WHERE pi.version_sequence < 1000000000
GO

-- 12 cache model
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS InstanceId,
    COALESCE(NULLIF(CONVERT(nvarchar(512), mi.INSTANCE_NM), N''), CONVERT(nvarchar(512), mt.DISPLAY_NM)) AS Name,
    CAST(NULL AS nvarchar(512)) AS Tag,
    CAST(NULL AS nvarchar(512)) AS ConstructionCode,
    CAST(NULL AS nvarchar(512)) AS BimId,
    TRY_CAST(mi.MODEL_TYPE AS int) AS ModelType,
    TRY_CAST(mi.COMPOSITE_YN AS int) AS CompositeYn,
    TRY_CONVERT(uniqueidentifier, mi.PARENT_INSTANCE_ID) AS ParentInstanceId,
    CONVERT(nvarchar(256), mt.FAMILY_KEY) AS FamilyKey,
    TRY_CAST(mt.BIM_YN AS int) AS BimYn,
    TRY_CAST(div0.DIVISION_TYPE AS int) AS DivisionType,
    CONVERT(nvarchar(256), div0.DIVISION_VALUE) AS DivisionName,
    CONVERT(nvarchar(256), sub1.DIVISION_VALUE) AS Sub1Name,
    CONVERT(nvarchar(256), sub2.DIVISION_VALUE) AS Sub2Name,
    TRY_CAST(sub1.DIVISION_TYPE AS int) AS Sub1DivisionType,
    CAST(ROUND(mi.POSX, 1) AS float) AS PosX,
    CAST(ROUND(mi.POSY, 1) AS float) AS PosY,
    CAST(ROUND(mi.POSZ, 1) AS float) AS PosZ,
    ISNULL(TRY_CAST(ROUND(mi.POSZ, 0) AS bigint), 0) AS Pz,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_MODELINSTANCES mi
LEFT JOIN [remote_monami].[monami].dbo.TB_MODELTEMPLATES mt
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID AND mt.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION div0
    ON div0.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.DIVISION_ID) AND div0.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION sub1
    ON sub1.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_1_ID) AND sub1.version_sequence < 1000000000
LEFT JOIN [remote_monami].[monami].dbo.TB_DIVISION sub2
    ON sub2.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_2_ID) AND sub2.version_sequence < 1000000000
WHERE mi.version_sequence < 1000000000
GO

-- 13 cache attribute
SELECT
    ISNULL(TRY_CONVERT(uniqueidentifier, a.OWNER_ID), CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')) AS OwnerId,
    CONVERT(nvarchar(256), a.ATTRIBUTE_NM) AS Name,
    CONVERT(nvarchar(4000), a.ATTRIBUTE_VAL) AS Value,
    CAST(0 AS int) AS OperationType
FROM [remote_monami].[monami].dbo.TB_ATTRIBUTES a
WHERE a.version_sequence < 1000000000
  AND a.ATTRIBUTE_NM IN (N'Element ID_Number', N'__MODULE_INFO', N'tag', N'Construction-code', N'ConnectionTag', N'S5_LATERAL_NUMBER', N'HookupNumber')
GO

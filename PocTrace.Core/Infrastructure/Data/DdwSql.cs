using System;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// DDW 그래프 SQL. TbStar 컬럼: TB_MODELINSTANCES(INSTANCE_ID, INSTANCE_NM, POSZ, MODEL_TYPE,
    /// MODEL_TEMPLATE_ID, COMPOSITE_YN, PARENT_INSTANCE_ID, OPERATIONTYPE),
    /// TB_MODELTEMPLATES(DISPLAY_NM, FAMILY_KEY, BIM_YN, DIVISION_ID, SUBDIVISION_1_ID, SUBDIVISION_2_ID),
    /// TB_DIVISION(DIVISION_TYPE, DIVISION_VALUE), TB_POCINSTANCES, TB_POCTEMPLATES, TB_PIPESIZE,
    /// TB_UTILITIES, TB_UTILITY_GROUPS, TB_PIPEINSTANCES(LENGTH1, PIPE_TYPE, UTILITY_ID, USER_ID, LAST_UPDATE),
    /// TB_NEW_USERS, TB_NEW_USERGROUPS(USER_GROUP_NAME), TB_ATTRIBUTES(Element ID_Number, __MODULE_INFO).
    /// WebDdw 추가 컬럼만: modelinstances.tag, s5_eqcode, construction_code, elementId_number,
    /// modeltemplates.Subdivision_1_name, Subdivision_2_name, pocinstances.tag, connectionTag.
    /// live는 OPERATIONTYPE &lt; 2. VERSIONS 조인, TOP 1, ×1000 은 넣지 않는다.
    /// </summary>
    public static class DdwSql
    {
        public static string SelectModels(string quotedDb, SchemaKind kind)
        {
            string name = "COALESCE(NULLIF(mi.INSTANCE_NM, ''), mt.DISPLAY_NM)";
            string tag = "CAST(NULL AS nvarchar(512))";
            string construction = "CAST(NULL AS nvarchar(512))";
            string bim = "CAST(NULL AS nvarchar(512))";
            string sub1 = "sub1.DIVISION_VALUE";
            string sub2 = "sub2.DIVISION_VALUE";
            if (kind == SchemaKind.WebDdw)
            {
                tag = "CASE WHEN mt.BIM_YN = 0 THEN mi.[tag] WHEN mt.BIM_YN = 1 THEN COALESCE(NULLIF(mi.[s5_eqcode], ''), mi.[tag]) ELSE mi.[tag] END";
                construction = "mi.[construction_code]";
                bim = "mi.[elementId_number]";
                sub1 = "COALESCE(NULLIF(mt.[Subdivision_1_name], ''), sub1.DIVISION_VALUE)";
                sub2 = "COALESCE(NULLIF(mt.[Subdivision_2_name], ''), sub2.DIVISION_VALUE)";
            }
            else if (kind != SchemaKind.TbStar)
            {
                throw new InvalidOperationException("DDW model SQL is only for TbStar or WebDdw.");
            }

            return @"
SELECT
    mi.INSTANCE_ID AS InstanceId,
    " + name + @" AS Name,
    " + tag + @" AS Tag,
    " + construction + @" AS ConstructionCode,
    " + bim + @" AS BimId,
    mi.MODEL_TYPE AS ModelType,
    mi.COMPOSITE_YN AS CompositeYn,
    mi.PARENT_INSTANCE_ID AS ParentInstanceId,
    mt.FAMILY_KEY AS FamilyKey,
    div0.DIVISION_TYPE AS DivisionType,
    " + sub1 + @" AS Sub1Name,
    " + sub2 + @" AS Sub2Name,
    sub1.DIVISION_TYPE AS Sub1DivisionType,
    CAST(ROUND(mi.POSZ, 0) AS bigint) AS Pz,
    mi.OPERATIONTYPE AS OperationType
FROM " + T(quotedDb, "TB_MODELINSTANCES") + @" mi
LEFT JOIN " + T(quotedDb, "TB_MODELTEMPLATES") + @" mt
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID AND mt.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" div0
    ON div0.DIVISION_ID = TRY_CAST(mt.DIVISION_ID AS uniqueidentifier) AND div0.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" sub1
    ON sub1.DIVISION_ID = TRY_CAST(mt.SUBDIVISION_1_ID AS uniqueidentifier) AND sub1.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" sub2
    ON sub2.DIVISION_ID = TRY_CAST(mt.SUBDIVISION_2_ID AS uniqueidentifier) AND sub2.OPERATIONTYPE < 2
WHERE mi.OPERATIONTYPE < 2";
        }

        public static string SelectModelPocs(string quotedDb, SchemaKind kind)
        {
            string tag = "CAST(NULL AS nvarchar(512))";
            string connectionTag = "CAST(NULL AS nvarchar(512))";
            if (kind == SchemaKind.WebDdw)
            {
                tag = "p.[tag]";
                connectionTag = "p.[connectionTag]";
            }
            else if (kind != SchemaKind.TbStar)
            {
                throw new InvalidOperationException("DDW poc SQL is only for TbStar or WebDdw.");
            }

            return @"
SELECT
    p.INSTANCE_ID AS InstanceId,
    p.OWNER_INSTANCE_ID AS OwnerInstanceId,
    p.CONNECTED_POC_ID AS ConnectedPocId,
    p.OWNER_TYPE AS OwnerType,
    p.CONNECTION_ORDER AS ConnectionOrder,
    p.POC_SHAPE AS PocShape,
    p.DIAMETER AS Diameter,
    p.WIDTH AS Width,
    psz.DIAMETER AS TemplateDiameter,
    psz.WIDTH AS TemplateWidth,
    iu.UTILITY_NM AS InstanceUtilityName,
    tu.UTILITY_NM AS TemplateUtilityName,
    ig.UTILITY_GROUP_NM AS InstanceUtilityGroupName,
    tg.UTILITY_GROUP_NM AS TemplateUtilityGroupName,
    pt.POC_TEMPLATE_NM AS PocTemplateName,
    pt.MEMO AS Memo,
    " + tag + @" AS Tag,
    " + connectionTag + @" AS ConnectionTag,
    p.OPERATIONTYPE AS OperationType,
    tgt.INSTANCE_ID AS TargetInstanceId,
    tgt.OWNER_TYPE AS TargetOwnerType,
    tgt.OWNER_INSTANCE_ID AS TargetOwnerInstanceId
FROM " + T(quotedDb, "TB_POCINSTANCES") + @" p
LEFT JOIN " + T(quotedDb, "TB_POCTEMPLATES") + @" pt
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID AND pt.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_PIPESIZE") + @" psz
    ON psz.PIPESIZE_ID = COALESCE(p.PIPESIZE_ID, pt.PIPESIZE_ID) AND psz.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" iu
    ON iu.UTILITY_ID = p.UTILITY_ID AND iu.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" tu
    ON tu.UTILITY_ID = pt.UTILITY_ID AND tu.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_UTILITY_GROUPS") + @" ig
    ON ig.UTILITY_GROUP_ID = iu.UTILITY_GROUP_ID AND ig.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_UTILITY_GROUPS") + @" tg
    ON tg.UTILITY_GROUP_ID = tu.UTILITY_GROUP_ID AND tg.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_POCINSTANCES") + @" tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.OPERATIONTYPE < 2
WHERE p.OPERATIONTYPE < 2 AND p.OWNER_TYPE = 768";
        }

        public static string SelectPipePocs(string quotedDb)
        {
            return @"
SELECT
    p.INSTANCE_ID AS InstanceId,
    p.OWNER_INSTANCE_ID AS OwnerInstanceId,
    p.CONNECTED_POC_ID AS ConnectedPocId,
    p.OWNER_TYPE AS OwnerType,
    p.CONNECTION_ORDER AS ConnectionOrder,
    p.OPERATIONTYPE AS OperationType,
    tgt.INSTANCE_ID AS TargetInstanceId,
    tgt.OWNER_TYPE AS TargetOwnerType,
    tgt.OWNER_INSTANCE_ID AS TargetOwnerInstanceId
FROM " + T(quotedDb, "TB_POCINSTANCES") + @" p
LEFT JOIN " + T(quotedDb, "TB_POCINSTANCES") + @" tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt.OPERATIONTYPE < 2
WHERE p.OPERATIONTYPE < 2 AND p.OWNER_TYPE = 256";
        }

        public static string SelectPipes(string quotedDb)
        {
            return @"
SELECT
    pi.INSTANCE_ID AS InstanceId,
    pi.PIPE_TYPE AS PipeType,
    CASE WHEN pi.LENGTH1 IS NULL OR pi.LENGTH1 <= 0 THEN 0 ELSE ROUND(pi.LENGTH1, 1) END AS Length,
    u.UTILITY_NM AS UtilityName,
    pi.LAST_UPDATE AS OpTime,
    nu.USER_NAME AS UserName,
    ug.USER_GROUP_NAME AS CompanyName,
    CAST(NULL AS nvarchar(512)) AS Tag,
    pi.OPERATIONTYPE AS OperationType
FROM " + T(quotedDb, "TB_PIPEINSTANCES") + @" pi
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" u
    ON u.UTILITY_ID = pi.UTILITY_ID AND u.OPERATIONTYPE < 2
LEFT JOIN " + T(quotedDb, "TB_NEW_USERS") + @" nu
    ON nu.USER_ID = pi.USER_ID
LEFT JOIN " + T(quotedDb, "TB_NEW_USERGROUPS") + @" ug
    ON ug.USER_GROUP_ID = nu.USER_GROUP_ID
WHERE pi.OPERATIONTYPE < 2";
        }

        public static string SelectAttributes(string quotedDb)
        {
            return @"
SELECT
    a.OWNER_ID AS OwnerId,
    a.ATTRIBUTE_NM AS Name,
    a.ATTRIBUTE_VAL AS Value,
    a.OPERATIONTYPE AS OperationType
FROM " + T(quotedDb, "TB_ATTRIBUTES") + @" a
WHERE a.OPERATIONTYPE < 2
  AND a.ATTRIBUTE_NM IN (N'Element ID_Number', N'__MODULE_INFO')";
        }

        public static string ColumnExists(string quotedDb)
        {
            return "SELECT COUNT(1) FROM " + quotedDb + ".INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";
        }

        private static string T(string quotedDb, string table)
        {
            return quotedDb + ".dbo." + table;
        }
    }
}

using System;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// DDW 그래프 SQL. live는 version_sequence &lt; 1000000000 이다. OPERATIONTYPE 열은 이 스키마에 없다.
    /// id 열은 varbinary(16) 이다. Dapper가 Guid로 읽도록 TRY_CONVERT(uniqueidentifier) 한다.
    /// 그 변환은 new Guid(byte[16]) 과 같은 바이트 순서다.
    /// 숫자와 길이는 int, float 로 맞춘다. decimal, bigint, bit 이 그대로 오면 다음 열에서 같은 파싱 오류가 난다.
    /// 배관 POC는 poc_template_id 가 0인 날배관만 본다. owner_type 256 은 보류다.
    /// tb_pipeinstances 에는 user_id, last_update 가 없다. S5D PipeLog 의 작업자·회사·시각 열을 붙이지 않는다.
    /// </summary>
    public static class DdwSql
    {
        public const string Live = "version_sequence < 1000000000";
        public const string ZeroGuid = "00000000-0000-0000-0000-000000000000";
        private const string Op = "CAST(0 AS int) AS OperationType";

        public static string SelectModels(string quotedDb, SchemaKind kind)
        {
            string name = "COALESCE(NULLIF(" + Text("mi.INSTANCE_NM", 512) + ", N''), " + Text("mt.DISPLAY_NM", 512) + ")";
            string tag = "CAST(NULL AS nvarchar(512))";
            string construction = "CAST(NULL AS nvarchar(512))";
            string bim = "CAST(NULL AS nvarchar(512))";
            string sub1 = Text("sub1.DIVISION_VALUE", 256);
            string sub2 = Text("sub2.DIVISION_VALUE", 256);
            if (kind == SchemaKind.WebDdw)
            {
                tag = "CASE WHEN mt.BIM_YN = 0 THEN " + Text("mi.[tag]", 512)
                    + " WHEN mt.BIM_YN = 1 THEN COALESCE(NULLIF(" + Text("mi.[s5_eqcode]", 512) + ", N''), " + Text("mi.[tag]", 512) + ") ELSE "
                    + Text("mi.[tag]", 512) + " END";
                construction = Text("mi.[construction_code]", 512);
                bim = Text("mi.[elementId_number]", 512);
                sub1 = "COALESCE(NULLIF(" + Text("mt.[Subdivision_1_name]", 256) + ", N''), " + Text("sub1.DIVISION_VALUE", 256) + ")";
                sub2 = "COALESCE(NULLIF(" + Text("mt.[Subdivision_2_name]", 256) + ", N''), " + Text("sub2.DIVISION_VALUE", 256) + ")";
            }
            else if (kind != SchemaKind.TbStar)
            {
                throw new InvalidOperationException("DDW model SQL is only for TbStar or WebDdw.");
            }

            return @"
SELECT
    " + GuidOf("mi.INSTANCE_ID") + @" AS InstanceId,
    " + name + @" AS Name,
    " + tag + @" AS Tag,
    " + construction + @" AS ConstructionCode,
    " + bim + @" AS BimId,
    " + IntNull("mi.MODEL_TYPE") + @" AS ModelType,
    " + IntNull("mi.COMPOSITE_YN") + @" AS CompositeYn,
    " + GuidNull("mi.PARENT_INSTANCE_ID") + @" AS ParentInstanceId,
    " + Text("mt.FAMILY_KEY", 256) + @" AS FamilyKey,
    " + IntNull("div0.DIVISION_TYPE") + @" AS DivisionType,
    " + sub1 + @" AS Sub1Name,
    " + sub2 + @" AS Sub2Name,
    " + IntNull("sub1.DIVISION_TYPE") + @" AS Sub1DivisionType,
    ISNULL(TRY_CAST(ROUND(mi.POSZ, 0) AS bigint), 0) AS Pz,
    " + Op + @"
FROM " + T(quotedDb, "TB_MODELINSTANCES") + @" mi
LEFT JOIN " + T(quotedDb, "TB_MODELTEMPLATES") + @" mt
    ON mt.MODEL_TEMPLATE_ID = mi.MODEL_TEMPLATE_ID AND mt." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" div0
    ON div0.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.DIVISION_ID) AND div0." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" sub1
    ON sub1.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_1_ID) AND sub1." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_DIVISION") + @" sub2
    ON sub2.DIVISION_ID = TRY_CONVERT(uniqueidentifier, mt.SUBDIVISION_2_ID) AND sub2." + Live + @"
WHERE mi." + Live;
        }

        public static string SelectModelPocs(string quotedDb, SchemaKind kind)
        {
            string tag = "CAST(NULL AS nvarchar(512))";
            string connectionTag = "CAST(NULL AS nvarchar(512))";
            if (kind == SchemaKind.WebDdw)
            {
                tag = Text("p.[tag]", 512);
                connectionTag = Text("p.[connectionTag]", 512);
            }
            else if (kind != SchemaKind.TbStar)
            {
                throw new InvalidOperationException("DDW poc SQL is only for TbStar or WebDdw.");
            }

            return @"
SELECT
    " + GuidOf("p.INSTANCE_ID") + @" AS InstanceId,
    " + GuidOf("p.OWNER_INSTANCE_ID") + @" AS OwnerInstanceId,
    " + GuidNull("p.CONNECTED_POC_ID") + @" AS ConnectedPocId,
    " + IntNull("p.OWNER_TYPE") + @" AS OwnerType,
    " + IntOf("p.CONNECTION_ORDER") + @" AS ConnectionOrder,
    " + IntOf("p.POC_SHAPE") + @" AS PocShape,
    " + FloatNull("p.DIAMETER") + @" AS Diameter,
    " + FloatNull("p.WIDTH") + @" AS Width,
    " + FloatNull("psz.DIAMETER") + @" AS TemplateDiameter,
    " + FloatNull("psz.WIDTH") + @" AS TemplateWidth,
    " + Text("iu.UTILITY_NM", 256) + @" AS InstanceUtilityName,
    " + Text("tu.UTILITY_NM", 256) + @" AS TemplateUtilityName,
    " + Text("ig.UTILITY_GROUP_NM", 256) + @" AS InstanceUtilityGroupName,
    " + Text("tg.UTILITY_GROUP_NM", 256) + @" AS TemplateUtilityGroupName,
    " + Text("pt.POC_TEMPLATE_NM", 512) + @" AS PocTemplateName,
    " + Text("pt.MEMO", 512) + @" AS Memo,
    " + tag + @" AS Tag,
    " + connectionTag + @" AS ConnectionTag,
    " + Op + @",
    " + GuidNull("tgt.INSTANCE_ID") + @" AS TargetInstanceId,
    " + IntNull("tgt.OWNER_TYPE") + @" AS TargetOwnerType,
    " + GuidNull("tgt.OWNER_INSTANCE_ID") + @" AS TargetOwnerInstanceId
FROM " + T(quotedDb, "TB_POCINSTANCES") + @" p
LEFT JOIN " + T(quotedDb, "TB_POCTEMPLATES") + @" pt
    ON pt.POC_TEMPLATE_ID = p.POC_TEMPLATE_ID AND pt." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_PIPESIZE") + @" psz
    ON psz.PIPESIZE_ID = COALESCE(p.PIPESIZE_ID, pt.PIPESIZE_ID) AND psz." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" iu
    ON iu.UTILITY_ID = p.UTILITY_ID AND iu." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" tu
    ON tu.UTILITY_ID = pt.UTILITY_ID AND tu." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_UTILITY_GROUPS") + @" ig
    ON ig.UTILITY_GROUP_ID = iu.UTILITY_GROUP_ID AND ig." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_UTILITY_GROUPS") + @" tg
    ON tg.UTILITY_GROUP_ID = tu.UTILITY_GROUP_ID AND tg." + Live + @"
LEFT JOIN " + T(quotedDb, "TB_POCINSTANCES") + @" tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt." + Live + @"
WHERE p." + Live + " AND " + IntOf("p.OWNER_TYPE") + " = 768";
        }

        public static string SelectPipePocs(string quotedDb)
        {
            return @"
SELECT
    " + GuidOf("p.INSTANCE_ID") + @" AS InstanceId,
    " + GuidOf("p.OWNER_INSTANCE_ID") + @" AS OwnerInstanceId,
    " + GuidNull("p.CONNECTED_POC_ID") + @" AS ConnectedPocId,
    " + IntNull("p.OWNER_TYPE") + @" AS OwnerType,
    " + IntOf("p.CONNECTION_ORDER") + @" AS ConnectionOrder,
    " + Op + @",
    " + GuidNull("tgt.INSTANCE_ID") + @" AS TargetInstanceId,
    " + IntNull("tgt.OWNER_TYPE") + @" AS TargetOwnerType,
    " + GuidNull("tgt.OWNER_INSTANCE_ID") + @" AS TargetOwnerInstanceId
FROM " + T(quotedDb, "TB_POCINSTANCES") + @" p
LEFT JOIN " + T(quotedDb, "TB_POCINSTANCES") + @" tgt
    ON tgt.INSTANCE_ID = p.CONNECTED_POC_ID AND tgt." + Live + @"
WHERE p." + Live + @"
  AND " + GuidNull("p.POC_TEMPLATE_ID") + " = CONVERT(uniqueidentifier, '" + ZeroGuid + "')";
        }

        public static string SelectPipes(string quotedDb)
        {
            return @"
SELECT
    " + GuidOf("pi.INSTANCE_ID") + @" AS InstanceId,
    " + IntOf("pi.PIPE_TYPE") + @" AS PipeType,
    CAST(CASE WHEN pi.LENGTH2 IS NULL OR pi.LENGTH2 <= 0 THEN 0 ELSE ROUND(pi.LENGTH2, 1) END AS float) AS Length,
    " + Text("u.UTILITY_NM", 256) + @" AS UtilityName,
    CAST(NULL AS datetime2) AS OpTime,
    CAST(NULL AS nvarchar(256)) AS UserName,
    CAST(NULL AS nvarchar(256)) AS CompanyName,
    CAST(NULL AS nvarchar(512)) AS Tag,
    " + Op + @"
FROM " + T(quotedDb, "TB_PIPEINSTANCES") + @" pi
LEFT JOIN " + T(quotedDb, "TB_UTILITIES") + @" u
    ON u.UTILITY_ID = pi.UTILITY_ID AND u." + Live + @"
WHERE pi." + Live;
        }

        public static string SelectAttributes(string quotedDb)
        {
            return @"
SELECT
    " + GuidOf("a.OWNER_ID") + @" AS OwnerId,
    " + Text("a.ATTRIBUTE_NM", 256) + @" AS Name,
    " + Text("a.ATTRIBUTE_VAL", 4000) + @" AS Value,
    " + Op + @"
FROM " + T(quotedDb, "TB_ATTRIBUTES") + @" a
WHERE a." + Live + @"
  AND a.ATTRIBUTE_NM IN (N'Element ID_Number', N'__MODULE_INFO', N'tag', N'Construction-code', N'ConnectionTag', N'S5_LATERAL_NUMBER')";
        }

        public static string ColumnExists(string quotedDb)
        {
            return "SELECT COUNT(1) FROM " + quotedDb + ".INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";
        }

        private static string GuidOf(string expr)
        {
            return "ISNULL(TRY_CONVERT(uniqueidentifier, " + expr + "), CONVERT(uniqueidentifier, '" + ZeroGuid + "'))";
        }

        private static string GuidNull(string expr)
        {
            return "TRY_CONVERT(uniqueidentifier, " + expr + ")";
        }

        private static string IntOf(string expr)
        {
            return "ISNULL(TRY_CAST(" + expr + " AS int), 0)";
        }

        private static string IntNull(string expr)
        {
            return "TRY_CAST(" + expr + " AS int)";
        }

        private static string FloatNull(string expr)
        {
            return "TRY_CAST(" + expr + " AS float)";
        }

        private static string Text(string expr, int length)
        {
            return "CONVERT(nvarchar(" + length.ToString() + "), " + expr + ")";
        }

        private static string T(string quotedDb, string table)
        {
            return quotedDb + ".dbo." + table;
        }
    }
}

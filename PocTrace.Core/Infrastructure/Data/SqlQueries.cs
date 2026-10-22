using System;

namespace PocTrace.Core.Infrastructure.Data
{
    public static class SqlQueries
    {
        // [1] Node 조회: Division 및 S1, S2 + BimId
        // BIM Group Name(ConfigurationLog) OUTER APPLY는 캐시 생성 타임아웃으로 보류(VOC 2026-08).
        public static string GetSelectNodes(string dbName)
        {
            return $@"
            /* [Equipment] Division=0 */
            SELECT 
                A.InstanceID AS InstanceId,
                WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID) AS InstanceNumericId,
                A.Tag        AS Tag, 
                A.Name       AS Name, 
                0            AS Type,
                B.Division   AS Division,
                S1.Name      AS Sub1Name,
                S2.Name      AS Sub2Name, 
                B.QCode,
                A.CONSTRUCTIONCODE AS ConstructionCode,
                CAST(A.BIMID AS NVARCHAR(100)) AS BimId,
                CAST(ROUND(A.PY*1000,0) AS BIGINT) AS PZ
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S2 WITH(NOLOCK) ON B.Subdivision2 = S2.SubdivID
            WHERE B.Division = 0 
              AND B.Subdivision1 IS NOT NULL
              AND A.OperationType < 2 and B.OperationType < 2
              AND S1.OperationType < 2 and S2.OperationType < 2
              AND (
                    A.CONSTRUCTIONCODE IS NULL
                    OR A.CONSTRUCTIONCODE NOT LIKE '%Delete%'
                  )
              AND S1.Name not in ('BIM', '3-template', '5-electrical')
              AND CAST(ROUND(A.PY*1000,0) AS BIGINT) < 100000

            UNION ALL

            /* [Damper] Type=4 */
            SELECT 
                A.InstanceID,
                WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID),
                A.Tag, ISNULL(A.Name, S1.Name), 4,
                B.Division, S1.Name, S2.Name, B.QCode,
                A.CONSTRUCTIONCODE,
                CAST(A.BIMID AS NVARCHAR(100)),
                CAST(ROUND(A.PY*1000,0) AS BIGINT) AS PZ
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S2 WITH(NOLOCK) ON B.Subdivision2 = S2.SubdivID
            WHERE A.OperationType < 2 and B.OperationType < 2 AND S1.Name = 'Damper'

            UNION ALL

            /* [Nozzle] Type=1 */
            SELECT 
                A.InstanceID,
                WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID),
                A.Tag, A.Name, 1,
                B.Division, S1.Name, S2.Name, B.QCode,
                A.CONSTRUCTIONCODE,
                CAST(A.BIMID AS NVARCHAR(100)),
                CAST(ROUND(A.PY*1000,0) AS BIGINT) AS PZ
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S2 WITH(NOLOCK) ON B.Subdivision2 = S2.SubdivID
            WHERE A.OperationType < 2 and B.OperationType < 2
              AND S1.Name = 'TAKE-OFF'
              AND (S2.Name LIKE '%Nozzle%' or S2.Name LIKE '%valvebox%')
              AND (S1.Division <> 0)
              AND (S1.Name IS NULL OR S1.Name <> 'Damper')

            UNION ALL

            /* [Fitting] Type=3 */
            SELECT 
                A.InstanceID,
                WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID),
                A.Tag, ISNULL(A.Name, S1.Name), 3,
                B.Division, S1.Name, S2.Name, B.QCode,
                A.CONSTRUCTIONCODE,
                CAST(A.BIMID AS NVARCHAR(100)),
                CAST(ROUND(A.PY*1000,0) AS BIGINT) AS PZ
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S2 WITH(NOLOCK) ON B.Subdivision2 = S2.SubdivID
            WHERE A.OperationType < 2 and B.OperationType < 2
              AND B.Division in (0, 3)
              AND S1.Name not in ('Damper', '1-MAIN TOOL', '2-SUB TOOL', '3-TEMPLATE', '4-PCS', '5-ELECTRICAL', '6-GCS-S.GAS', '6-GCS-CHEMICAL')
              AND A.Name NOT LIKE '%Nozzle%'
            ";
        }

        // [2] POC 조회: Utility, Group, Size, Tag 추가
        // [수정] Join 조건 변경 (PocID = PointID) 및 Name 추가
        public static string GetSelectPocs(string dbName)
        {
            return $@"
                        SELECT 
                A.InstanceID   AS InstanceId, 
                A.OwnerID      AS OwnerId, 
                A.Outlet       AS Outlet, 
                A.ReferenceTag AS ReferenceTag,
                A.PocID        AS PointID,       -- [신규] POC 고유 ID (DynamicProperties 조회용)
        
                A.Tag          AS Tag,           -- S: Connector Tag
                B.Name         AS PocName,       -- S: Connector Name
                B.Information  AS Information, 
                FLOOR(ISNULL(A.ExtentOne, 0) * 1000) AS Size, 
                C.Name         AS UtilityName,   -- S: Utility
                D.Name         AS UtilityGroupName, -- S: Utility Group
        
                -- [신규] CompositeObjectItemLog의 Module 정보 추가
                -- LEFT JOIN으로 기존 행 수 유지, MAX로 중복 방지
                (SELECT MAX(E.Module) 
                 FROM {dbName}.dbo.CompositeObjectItemLog E WITH(NOLOCK)
                 WHERE E.itemid = A.compositeitemid 
                   AND E.Module IS NOT NULL 
                   AND E.Module <> '') AS ModuleName

            FROM {dbName}.dbo.ConnectionPointInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectInstanceLog O WITH(NOLOCK) ON A.OwnerID = O.InstanceID
    
            -- [수정] 정확한 Join 조건 적용
            JOIN {dbName}.dbo.ConnectionPointLog B WITH(NOLOCK) ON A.PocID = B.PointID
    
            -- [복구] Utility 관련 조인 활성화
            LEFT JOIN {dbName}.dbo.UtilityLog C WITH(NOLOCK) ON B.UtilityID = C.UtilityID
            LEFT JOIN {dbName}.dbo.UtilityGroupLog D WITH(NOLOCK) ON C.GroupID = D.GroupID
    
            WHERE O.OperationType < 2 and A.OperationType < 2 --and C.Name is not null
            --AND CAST(ROUND(A.PY*1000,0) AS BIGINT) < 100000

            --Poc류 X1:2,709,774 EA (26.05.27) 
            ";
        }

        public static string GetSelectPipes(string dbName)
        {
            // [수정] USERLOG와 조인하여 작업자 정보 추가
            return $@"SELECT 
                A.PipeID, 
                A.Outlet1, 
                A.Outlet2, 
                A.Outlet3, 
                CASE WHEN A.Length <= 0 THEN 0 ELSE ROUND(A.Length *1000, 1) END Length, 
                B.NAME AS UtilityName,
                A.OPTIME AS OpTime,          -- [신규] 작업날짜
                U.NAME AS UserName,          -- [신규] 작업자 이름
                U.COMPANYNAME AS CompanyName -- [신규] 작업자 귀속 회사
            FROM {dbName}.dbo.PipeLog A WITH(NOLOCK)
            LEFT JOIN {dbName}.dbo.UtilityLog B WITH(NOLOCK) ON A.UtilityID = B.UtilityID AND B.OperationType < 2
            LEFT JOIN {dbName}.dbo.USERLOG U WITH(NOLOCK) ON A.USER_ID = U.USERID
            WHERE A.OperationType < 2 
            --and (outlet1 is not null or outlet2 is not null)
            and A.Origin = 0 --AND CAST(ROUND(A.PY*1000,0) AS BIGINT) < 100000";
        }

        public static string GetSelectTakeoffs(string dbName)
        {
            return $@"SELECT TakeoffID AS TakeoffId, Tag, PipeID AS PipeId
                      FROM {dbName}.dbo.TakeoffLog WITH(NOLOCK)
                      WHERE operationType < 2";
        }

        // [New] 전체 설비(Equipment) 목록 조회용 (스케줄러 전용)
        // 1-% (Main Tool) + 6-% (GCS) — VOC: Stick/GCS도 Full Trace 대상
        public static string GetSelectAllEquipments(string dbName)
        {
            return $@"
            SELECT DISTINCT
                A.Tag,
                A.CONSTRUCTIONCODE AS ConstructionCode
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            WHERE B.Division = 0  -- 0: Equipment
              AND A.OperationType < 2
              AND B.OperationType < 2
              AND S1.OperationType < 2
              AND A.Tag IS NOT NULL
              AND A.Tag <> ''
              AND (S1.Name LIKE '1-%' OR S1.Name LIKE '6-%')
              AND (
                    A.CONSTRUCTIONCODE IS NULL
                    OR (
                      A.CONSTRUCTIONCODE LIKE '%-%'
                      AND A.CONSTRUCTIONCODE NOT LIKE '%Delete%'
                    )
                  )
            ORDER BY A.Tag
            ";
        }

        // 💡 [신규] GUID와 매칭되는 Hookup Number를 가져오는 쿼리
        // 반환 구조: SELECT GuidColumn, HookupNumberColumn FROM ...f
        public static string GetDynamicPropertiesQuery(string dbName)
        {
                return $@"
                SELECT
                OWNERID as PointID,
                HOOKUPNUMBER as HookupNumber,
                POCNO as PocNo,
                CONNECTORUSE as ConnectorUse
                FROM WEBDB.WEBDEV5D.W5D_DYNAMICPROPERTIESINSTANCELOG_{dbName}
                WHERE operationType < 2
                  and (HOOKUPNUMBER is not null or POCNO is not null or CONNECTORUSE is not null)
            ";
        
        }

        /// <summary>
        /// BIM/Group sidecar용. ConfigurationGroupLog: GroupID·Name·ParentID.
        /// OperationType &lt; 2, 동일 GroupID는 OPTIME 최신 1건.
        /// JOIN 없음 — GraphCache GetSelectNodes에 OUTER APPLY 금지.
        /// </summary>
        public static string GetSelectBimGroups(string dbName)
        {
            return $@"
            SELECT
                GroupId,
                Name,
                ParentId
            FROM (
                SELECT
                    GroupID AS GroupId,
                    LTRIM(RTRIM(Name)) AS Name,
                    ParentID AS ParentId,
                    OPTIME,
                    ROW_NUMBER() OVER (
                        PARTITION BY GroupID
                        ORDER BY OPTIME DESC
                    ) AS rn
                FROM {dbName}.dbo.ConfigurationGroupLog WITH (NOLOCK)
                WHERE OperationType < 2
                  AND Name IS NOT NULL
                  AND LTRIM(RTRIM(Name)) <> ''
                  AND Name NOT LIKE '%capa%'
            ) x
            WHERE rn = 1
            ";
        }

        /// <summary>하위 호환: Name만 필요하던 구 Admin/캐시용.</summary>
        public static string GetSelectBimGroupNames(string dbName)
        {
            return $@"
            SELECT Name AS BimGroupName
            FROM (
                SELECT
                    LTRIM(RTRIM(Name)) AS Name,
                    OPTIME,
                    ROW_NUMBER() OVER (
                        PARTITION BY LTRIM(RTRIM(Name))
                        ORDER BY OPTIME DESC
                    ) AS rn
                FROM {dbName}.dbo.ConfigurationGroupLog WITH (NOLOCK)
                WHERE OperationType < 2
                  AND Name IS NOT NULL
                  AND LTRIM(RTRIM(Name)) <> ''
                  AND Name NOT LIKE '%capa%'
            ) x
            WHERE rn = 1
            ";
        }

        /// <summary>
        /// 철거 검수 전용: Subdivision1 LIKE '3-%' (3-template 등) Equipment.
        /// GraphCache GetSelectNodes에는 넣지 않음 — POC Trace 부담/혼선 방지 (BimGroup sidecar와 동일 패턴).
        /// </summary>
        public static string GetSelectTemplateEquipments(string dbName)
        {
            return $@"
            SELECT 
                A.InstanceID AS InstanceId,
                WEBDB.WEBDEV5D.GetDotNetHashCode(A.InstanceID) AS InstanceNumericId,
                A.Tag        AS Tag, 
                A.Name       AS Name, 
                0            AS Type,
                B.Division   AS Division,
                S1.Name      AS Sub1Name,
                S2.Name      AS Sub2Name, 
                B.QCode,
                A.CONSTRUCTIONCODE AS ConstructionCode,
                CAST(A.BIMID AS NVARCHAR(100)) AS BimId,
                CAST(ROUND(A.PY*1000,0) AS BIGINT) AS PZ
            FROM {dbName}.dbo.ObjectInstanceLog A WITH(NOLOCK)
            JOIN {dbName}.dbo.ObjectLog B WITH(NOLOCK) ON A.ObjectID = B.ObjectID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S1 WITH(NOLOCK) ON B.Subdivision1 = S1.SubdivID
            LEFT JOIN {dbName}.dbo.SubdivisionLog S2 WITH(NOLOCK) ON B.Subdivision2 = S2.SubdivID
            WHERE B.Division = 0 
              AND B.Subdivision1 IS NOT NULL
              AND A.OperationType < 2 and B.OperationType < 2
              AND S1.OperationType < 2 and S2.OperationType < 2
              AND (
                    A.CONSTRUCTIONCODE IS NULL
                    OR A.CONSTRUCTIONCODE NOT LIKE '%Delete%'
                  )
              AND S1.Name LIKE '3-%'
              AND CAST(ROUND(A.PY*1000,0) AS BIGINT) < 100000
            ";
        }
    }
}

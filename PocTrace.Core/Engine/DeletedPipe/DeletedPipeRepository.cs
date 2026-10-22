using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Dapper;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// S5D 로그에서 Instance/Pipe/POC 별 최신 행만 읽는다.
    /// 최신 행의 OperationType이 2이고 OPTIME이 기준일 이상일 때만 삭제 대상으로 쓴다.
    /// 결과는 BinaryCache에 넣지 않는다.
    /// </summary>
    public sealed class DeletedPipeRepository
    {
        private const int BatchSize = 150;
        private const int CommandTimeoutSeconds = 600;

        private readonly SqlConnection _connection;
        private readonly string _db;
        private readonly DateTime _cutover;
        private readonly CancellationToken _cancellation;

        public DeletedPipeRepository(SqlConnection connection, string dbName, DateTime cutover, CancellationToken cancellation)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _db = SqlIdentifier.QuoteDatabase(dbName);
            _cutover = cutover;
            _cancellation = cancellation;
        }

        public List<DeletedEquipmentRow> LoadDeletedEquipment()
        {
            string sql = $@"
SELECT
    InstanceId, Tag, Name, OpTime, OperationType, Division, Sub1Name, Sub2Name
FROM (
    SELECT
        A.InstanceID AS InstanceId,
        A.Tag AS Tag,
        A.Name AS Name,
        A.OPTIME AS OpTime,
        A.OperationType AS OperationType,
        B.Division AS Division,
        S1.Name AS Sub1Name,
        S2.Name AS Sub2Name,
        ROW_NUMBER() OVER (PARTITION BY A.InstanceID ORDER BY A.OPTIME DESC) AS rn
    FROM {_db}.dbo.ObjectInstanceLog A WITH (NOLOCK)
    JOIN {_db}.dbo.ObjectLog B WITH (NOLOCK)
        ON A.ObjectID = B.ObjectID AND B.OperationType < 2
    LEFT JOIN {_db}.dbo.SubdivisionLog S1 WITH (NOLOCK)
        ON B.Subdivision1 = S1.SubdivID AND S1.OperationType < 2
    LEFT JOIN {_db}.dbo.SubdivisionLog S2 WITH (NOLOCK)
        ON B.Subdivision2 = S2.SubdivID AND S2.OperationType < 2
    WHERE B.Division = 0
      AND A.InstanceID IN (
            SELECT D.InstanceID
            FROM {_db}.dbo.ObjectInstanceLog D WITH (NOLOCK)
            WHERE D.OperationType = 2
              AND D.OPTIME >= @Cutover
      )
) x
WHERE rn = 1
  AND OperationType = 2
  AND OpTime >= @Cutover";

            return Query<DeletedEquipmentRow>(sql, new { Cutover = _cutover });
        }

        public List<DeletedPocRow> LoadPocsTouching(IList<Guid> ids)
        {
            var list = Clean(ids);
            if (list.Count == 0)
                return new List<DeletedPocRow>();

            string sql = $@"
SELECT
    InstanceId, OwnerId, Outlet, Tag, PocName, UtilityName, OpTime, OperationType
FROM (
    SELECT
        A.InstanceID AS InstanceId,
        A.OwnerID AS OwnerId,
        A.Outlet AS Outlet,
        A.Tag AS Tag,
        B.Name AS PocName,
        C.Name AS UtilityName,
        A.OPTIME AS OpTime,
        A.OperationType AS OperationType,
        ROW_NUMBER() OVER (PARTITION BY A.InstanceID ORDER BY A.OPTIME DESC) AS rn
    FROM {_db}.dbo.ConnectionPointInstanceLog A WITH (NOLOCK)
    LEFT JOIN {_db}.dbo.ConnectionPointLog B WITH (NOLOCK)
        ON A.PocID = B.PointID AND B.OperationType < 2
    LEFT JOIN {_db}.dbo.UtilityLog C WITH (NOLOCK)
        ON B.UtilityID = C.UtilityID AND C.OperationType < 2
    WHERE A.InstanceID IN (
            SELECT P.InstanceID
            FROM {_db}.dbo.ConnectionPointInstanceLog P WITH (NOLOCK)
            WHERE P.InstanceID IN @Ids
               OR P.Outlet IN @Ids
      )
) x
WHERE rn = 1";

            return QueryBatches<DeletedPocRow>(sql, list, null);
        }

        public List<DeletedPocRow> LoadPocsByOwner(IList<Guid> ownerIds)
        {
            var list = Clean(ownerIds);
            if (list.Count == 0)
                return new List<DeletedPocRow>();

            string sql = $@"
SELECT
    InstanceId, OwnerId, Outlet, Tag, PocName, UtilityName, OpTime, OperationType
FROM (
    SELECT
        A.InstanceID AS InstanceId,
        A.OwnerID AS OwnerId,
        A.Outlet AS Outlet,
        A.Tag AS Tag,
        B.Name AS PocName,
        C.Name AS UtilityName,
        A.OPTIME AS OpTime,
        A.OperationType AS OperationType,
        ROW_NUMBER() OVER (PARTITION BY A.InstanceID ORDER BY A.OPTIME DESC) AS rn
    FROM {_db}.dbo.ConnectionPointInstanceLog A WITH (NOLOCK)
    LEFT JOIN {_db}.dbo.ConnectionPointLog B WITH (NOLOCK)
        ON A.PocID = B.PointID AND B.OperationType < 2
    LEFT JOIN {_db}.dbo.UtilityLog C WITH (NOLOCK)
        ON B.UtilityID = C.UtilityID AND C.OperationType < 2
    WHERE A.OwnerID IN @Ids
) x
WHERE rn = 1";

            return QueryBatches<DeletedPocRow>(sql, list, null);
        }

        public List<DeletedPipeRow> LoadPipesTouching(IList<Guid> ids)
        {
            var list = Clean(ids);
            if (list.Count == 0)
                return new List<DeletedPipeRow>();

            string sql = $@"
SELECT
    PipeId, Outlet1, Outlet2, Outlet3, Length, UtilityName, OpTime, OperationType, UserName, CompanyName
FROM (
    SELECT
        A.PipeID AS PipeId,
        A.Outlet1 AS Outlet1,
        A.Outlet2 AS Outlet2,
        A.Outlet3 AS Outlet3,
        CASE WHEN A.Length IS NULL OR A.Length <= 0 THEN 0 ELSE A.Length END AS Length,
        B.NAME AS UtilityName,
        A.OPTIME AS OpTime,
        A.OperationType AS OperationType,
        U.NAME AS UserName,
        U.COMPANYNAME AS CompanyName,
        ROW_NUMBER() OVER (PARTITION BY A.PipeID ORDER BY A.OPTIME DESC) AS rn
    FROM {_db}.dbo.PipeLog A WITH (NOLOCK)
    LEFT JOIN {_db}.dbo.UtilityLog B WITH (NOLOCK)
        ON A.UtilityID = B.UtilityID AND B.OperationType < 2
    LEFT JOIN {_db}.dbo.USERLOG U WITH (NOLOCK)
        ON A.USER_ID = U.USERID
    WHERE A.Origin = 0
      AND A.PipeID IN (
            SELECT P.PipeID
            FROM {_db}.dbo.PipeLog P WITH (NOLOCK)
            WHERE P.Origin = 0
              AND (
                    P.PipeID IN @Ids
                    OR P.Outlet1 IN @Ids
                    OR P.Outlet2 IN @Ids
                    OR P.Outlet3 IN @Ids
                  )
      )
) x
WHERE rn = 1";

            return QueryBatches<DeletedPipeRow>(sql, list, null);
        }

        public List<DeletedNodeRow> LoadNodes(IList<Guid> ids)
        {
            var list = Clean(ids);
            if (list.Count == 0)
                return new List<DeletedNodeRow>();

            string sql = $@"
SELECT
    InstanceId, Tag, Name, OpTime, OperationType, Division, Sub1Name, Sub2Name
FROM (
    SELECT
        A.InstanceID AS InstanceId,
        A.Tag AS Tag,
        A.Name AS Name,
        A.OPTIME AS OpTime,
        A.OperationType AS OperationType,
        B.Division AS Division,
        S1.Name AS Sub1Name,
        S2.Name AS Sub2Name,
        ROW_NUMBER() OVER (PARTITION BY A.InstanceID ORDER BY A.OPTIME DESC) AS rn
    FROM {_db}.dbo.ObjectInstanceLog A WITH (NOLOCK)
    JOIN {_db}.dbo.ObjectLog B WITH (NOLOCK)
        ON A.ObjectID = B.ObjectID AND B.OperationType < 2
    LEFT JOIN {_db}.dbo.SubdivisionLog S1 WITH (NOLOCK)
        ON B.Subdivision1 = S1.SubdivID AND S1.OperationType < 2
    LEFT JOIN {_db}.dbo.SubdivisionLog S2 WITH (NOLOCK)
        ON B.Subdivision2 = S2.SubdivID AND S2.OperationType < 2
    WHERE A.InstanceID IN @Ids
) x
WHERE rn = 1";

            return QueryBatches<DeletedNodeRow>(sql, list, null);
        }

        public List<DeletedTakeoffRow> LoadTakeoffs(IList<Guid> ids)
        {
            var list = Clean(ids);
            if (list.Count == 0)
                return new List<DeletedTakeoffRow>();

            string sql = $@"
SELECT
    TakeoffId, Tag, PipeId, OpTime, OperationType
FROM (
    SELECT
        T.TakeoffID AS TakeoffId,
        T.Tag AS Tag,
        T.PipeID AS PipeId,
        T.OPTIME AS OpTime,
        T.operationType AS OperationType,
        ROW_NUMBER() OVER (PARTITION BY T.TakeoffID ORDER BY T.OPTIME DESC) AS rn
    FROM {_db}.dbo.TakeoffLog T WITH (NOLOCK)
    WHERE T.TakeoffID IN (
            SELECT H.TakeoffID
            FROM {_db}.dbo.TakeoffLog H WITH (NOLOCK)
            WHERE H.TakeoffID IN @Ids
               OR H.PipeID IN @Ids
      )
) x
WHERE rn = 1";

            return QueryBatches<DeletedTakeoffRow>(sql, list, null);
        }

        private List<T> Query<T>(string sql, object param)
        {
            var command = new CommandDefinition(sql, param, commandTimeout: CommandTimeoutSeconds, cancellationToken: _cancellation);
            return _connection.Query<T>(command).ToList();
        }

        private List<T> QueryBatches<T>(string sql, IList<Guid> ids, object extra)
        {
            var all = new List<T>();
            for (int offset = 0; offset < ids.Count; offset += BatchSize)
            {
                _cancellation.ThrowIfCancellationRequested();
                int count = Math.Min(BatchSize, ids.Count - offset);
                var batch = new List<Guid>(count);
                for (int i = 0; i < count; i++)
                    batch.Add(ids[offset + i]);

                var param = new DynamicParameters(extra);
                param.Add("Ids", batch);
                all.AddRange(Query<T>(sql, param));
            }
            return all;
        }

        private static List<Guid> Clean(IList<Guid> ids)
        {
            var list = new List<Guid>();
            if (ids == null)
                return list;
            var seen = new HashSet<Guid>();
            for (int i = 0; i < ids.Count; i++)
            {
                Guid id = ids[i];
                if (id == Guid.Empty)
                    continue;
                if (seen.Add(id))
                    list.Add(id);
            }
            return list;
        }
    }
}

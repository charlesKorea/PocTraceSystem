using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using PocTrace.Core.Infrastructure;
using Serilog;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>배관 한 개의 설계 날짜. 길이가 바뀐 기록만 센다.</summary>
  public sealed class PipeDesignDates
  {
    /// <summary>PipeLog 첫 기록 시각.</summary>
    public DateTime? FirstAt { get; set; }
    /// <summary>길이가 바뀐 마지막 기록 시각. 첫 기록(생성)도 길이가 생긴 것으로 본다.</summary>
    public DateTime? LastLengthChangeAt { get; set; }
  }

  /// <summary>PipeLog 한 행 (Length는 m).</summary>
  public sealed class PipeLogRow
  {
    public Guid PipeId { get; set; }
    public DateTime? OpTime { get; set; }
    public double Length { get; set; }
  }

  public interface IPipeDesignDateSource
  {
    /// <summary>배관 Guid → 설계 날짜. null이면 이력을 쓸 수 없다는 뜻이고, 호출자는 Pipe.OpTime으로 대신한다.</summary>
    IReadOnlyDictionary<Guid, PipeDesignDates> Load(string dbName, IReadOnlyCollection<Guid> pipeIds);
  }

  public sealed class NullPipeDesignDateSource : IPipeDesignDateSource
  {
    public static readonly NullPipeDesignDateSource Instance = new NullPipeDesignDateSource();

    public IReadOnlyDictionary<Guid, PipeDesignDates> Load(string dbName, IReadOnlyCollection<Guid> pipeIds)
    {
      return null;
    }
  }

  public static class PipeLengthHistory
  {
    /// <summary>같은 mm로 보는 허용 오차. 캐시도 0.1mm로 반올림해 읽는다.</summary>
    public const double LengthToleranceMm = 0.05;

    /// <summary>배관별로 시간순 이력을 훑어 첫 기록과 길이가 바뀐 마지막 기록을 구한다.</summary>
    public static Dictionary<Guid, PipeDesignDates> Summarize(IEnumerable<PipeLogRow> rows)
    {
      var result = new Dictionary<Guid, PipeDesignDates>();
      if (rows == null) return result;

      foreach (var group in rows.Where(r => r != null && r.OpTime.HasValue).GroupBy(r => r.PipeId))
      {
        var dates = new PipeDesignDates();
        double? prevMm = null;
        foreach (var row in group.OrderBy(r => r.OpTime.Value))
        {
          double mm = Math.Round(row.Length * 1000.0, 1);
          if (!dates.FirstAt.HasValue)
            dates.FirstAt = row.OpTime;
          if (!prevMm.HasValue || Math.Abs(mm - prevMm.Value) > LengthToleranceMm)
            dates.LastLengthChangeAt = row.OpTime;
          prevMm = mm;
        }
        result[group.Key] = dates;
      }
      return result;
    }
  }

  /// <summary>S5D PipeLog에서 배관별 길이 변경 이력을 읽는다. S5D가 아니거나 연결이 없으면 쓰지 않는다.</summary>
  public sealed class PipeDesignDateRepository : IPipeDesignDateSource
  {
    private const int BatchSize = 500;
    private const int CommandTimeoutSeconds = 300;

    private readonly string _connectionString;
    private readonly ILogger _log;

    public PipeDesignDateRepository(string connectionString, ILogger log = null)
    {
      _connectionString = connectionString;
      _log = log ?? Log.Logger;
    }

    public static IPipeDesignDateSource CreateFromConfig(ILogger log = null)
    {
      if (GlobalConfig.ActiveSchema != SchemaKind.S5d)
        return NullPipeDesignDateSource.Instance;
      string connectionString = SecretConfigStore.ConnectionString("PocTraceDB");
      if (string.IsNullOrWhiteSpace(connectionString))
        return NullPipeDesignDateSource.Instance;
      return new PipeDesignDateRepository(connectionString, log);
    }

    public IReadOnlyDictionary<Guid, PipeDesignDates> Load(string dbName, IReadOnlyCollection<Guid> pipeIds)
    {
      if (pipeIds == null || pipeIds.Count == 0)
        return new Dictionary<Guid, PipeDesignDates>();

      try
      {
        string db = SqlIdentifier.QuoteDatabase(dbName);
        string sql = $@"
SELECT
    A.PipeID AS PipeId,
    A.OPTIME AS OpTime,
    CASE WHEN A.Length IS NULL OR A.Length <= 0 THEN 0 ELSE A.Length END AS Length
FROM {db}.dbo.PipeLog A WITH (NOLOCK)
WHERE A.Origin = 0
  AND A.OperationType < 2
  AND A.PipeID IN @Ids";

        var ids = pipeIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var all = new Dictionary<Guid, PipeDesignDates>();
        using (var conn = new SqlConnection(_connectionString))
        {
          conn.Open();
          for (int offset = 0; offset < ids.Count; offset += BatchSize)
          {
            var batch = ids.GetRange(offset, Math.Min(BatchSize, ids.Count - offset));
            var rows = conn.Query<PipeLogRow>(sql, new { Ids = batch }, commandTimeout: CommandTimeoutSeconds);
            foreach (var kv in PipeLengthHistory.Summarize(rows))
              all[kv.Key] = kv.Value;
          }
        }
        _log.Information("[DesignProgress] {Db}: PipeLog length history for {Found}/{Total} pipes", dbName, all.Count, ids.Count);
        return all;
      }
      catch (Exception ex)
      {
        _log.Warning(ex, "[DesignProgress] {Db}: PipeLog 길이 이력을 읽지 못해 Pipe.OpTime으로 대신합니다.", dbName);
        return null;
      }
    }
  }
}

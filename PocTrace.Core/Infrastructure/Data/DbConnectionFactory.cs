using System;
using System.Data;
using PocTrace.Core.Infrastructure;
using System.Data.SqlClient;
using System.Data.SQLite;

namespace PocTrace.Core.Infrastructure.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateSourceConnection();
        IDbConnection CreateLocalConnection();
        bool IsSourceAvailable { get; } // DB 연결 정보가 있는지 확인하는 속성 추가
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _sourceConnectionString;
        private readonly string _localConnectionString;

        public bool IsSourceAvailable => !string.IsNullOrEmpty(_sourceConnectionString);

        public DbConnectionFactory()
        {
            // [수정] 생성자에서는 예외를 던지지 않고 null 처리만 합니다.
            // 로컬 환경에서는 SourceDb가 없을 수 있음을 허용합니다.
            _sourceConnectionString = SecretConfigStore.ConnectionString("SourceDb");

            string local = SecretConfigStore.ConnectionString("LocalDb");
            _localConnectionString = string.IsNullOrWhiteSpace(local)
                ? "Data Source=LocalCache.db;Version=3;"
                : local;
        }

        public IDbConnection CreateSourceConnection()
        {
            // [지연 검증] 실제로 연결을 만들려고 할 때 검사합니다.
            if (string.IsNullOrEmpty(_sourceConnectionString))
            {
                throw new InvalidOperationException(
                    "SourceDb connection string is missing. Put it in " + SecretConfigStore.FilePath);
            }
            return new SqlConnection(_sourceConnectionString);
        }

        public IDbConnection CreateLocalConnection()
        {
            return new SQLiteConnection(_localConnectionString);
        }
    }
}
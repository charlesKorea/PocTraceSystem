using System;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// DB 이름은 파라미터가 될 수 없다. TargetDatabases에 있는 이름만 괄호로 인용한다.
    /// </summary>
    public static class SqlIdentifier
    {
        public static string QuoteDatabase(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName))
                throw new ArgumentException("dbName is required", nameof(dbName));

            var allowed = GlobalConfig.TargetDatabases;
            string canonical = null;
            if (allowed != null)
            {
                for (int i = 0; i < allowed.Length; i++)
                {
                    if (string.Equals(allowed[i], dbName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        canonical = allowed[i];
                        break;
                    }
                }
            }
            if (canonical == null)
                throw new InvalidOperationException(
                    "Database is not in TargetDatabases. DDW SQL was not executed.");

            return "[" + canonical.Replace("]", "]]") + "]";
        }
    }
}

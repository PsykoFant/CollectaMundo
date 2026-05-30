using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class SharedMemoryDbFactory : IDbConnectionFactory, IDisposable
    {
        private readonly string _connectionString;
        private readonly SQLiteConnection _persistentConnection;

        public SharedMemoryDbFactory(string connectionString)
        {
            _connectionString = connectionString;
            _persistentConnection = new SQLiteConnection(connectionString);
            _persistentConnection.Open(); // keep the shared in-memory DB alive
        }

        public async Task<SQLiteConnection> OpenConnectionAsync()
        {
            var conn = new SQLiteConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public void Dispose()
        {
            try { _persistentConnection?.Dispose(); } catch { /* meh */ }
        }
        public static IDbConnectionFactory CreateInMemoryDbFactory(string dbName)
        {
            return new SharedMemoryDbFactory(TestSqliteConnectionStrings.SharedInMemory(dbName));
        }
    }
}

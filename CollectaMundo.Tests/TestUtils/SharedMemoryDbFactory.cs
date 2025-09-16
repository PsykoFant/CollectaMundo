using CollectaMundo.Data;
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
            // Unique name per test -> isolated in-memory DB
            // URI=True ensures the "file:dbname?..." string is parsed correctly
            var cs = $"Data Source=file:{dbName}?mode=memory&cache=shared;Version=3;URI=True;";
            return new SharedMemoryDbFactory(cs);
        }
    }
}

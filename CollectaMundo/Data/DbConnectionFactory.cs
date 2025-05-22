using CollectaMundo.ApplicationServices;
using System.Data;
using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IAppSettings _settings;
        private SQLiteConnection? _connection;

        public DbConnectionFactory(IAppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }


        public async Task<SQLiteConnection> OpenConnectionAsync()
        {
            if (_connection is null || _connection.State != ConnectionState.Open)
            {
                var cs = _settings
                            .ConnectionStrings
                            .SQLiteConnection
                            .Replace("{SQLitePath}",
                                     _settings.DatabaseSettings.SQLitePath);
                _connection = new SQLiteConnection(cs);
                await _connection.OpenAsync();
            }

            return _connection;
        }

        public void CloseConnection()
        {
            if (_connection?.State == ConnectionState.Open)
                _connection.Close();
        }
    }

}

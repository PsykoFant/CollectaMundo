using CollectaMundo.ApplicationServices;
using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public class DbConnectionFactory(IAppSettings settings) : IDbConnectionFactory
    {
        private readonly IAppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public async Task<SQLiteConnection> OpenConnectionAsync()
        {
            var cs = _settings
                        .ConnectionStrings
                        .SQLiteConnection
                        .Replace("{SQLitePath}", _settings.DatabaseSettings.SQLitePath);

            var conn = new SQLiteConnection(cs);
            await conn.OpenAsync();
            return conn;
        }
    }

}

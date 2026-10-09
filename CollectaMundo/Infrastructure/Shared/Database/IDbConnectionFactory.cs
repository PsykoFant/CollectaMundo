using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Shared.Database
{
    public interface IDbConnectionFactory
    {
        Task<SQLiteConnection> OpenConnectionAsync();
    }

}

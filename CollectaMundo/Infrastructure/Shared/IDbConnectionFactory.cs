using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Shared
{
    public interface IDbConnectionFactory
    {
        Task<SQLiteConnection> OpenConnectionAsync();
    }

}

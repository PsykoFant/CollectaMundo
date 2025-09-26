using System.Data.SQLite;

namespace CollectaMundo.Infrastructure
{
    public interface IDbConnectionFactory
    {
        Task<SQLiteConnection> OpenConnectionAsync();
    }

}

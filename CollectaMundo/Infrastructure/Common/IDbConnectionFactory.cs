using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Common
{
    public interface IDbConnectionFactory
    {
        Task<SQLiteConnection> OpenConnectionAsync();
    }

}

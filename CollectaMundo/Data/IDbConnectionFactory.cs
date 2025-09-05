using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface IDbConnectionFactory
    {
        Task<SQLiteConnection> OpenConnectionAsync();
    }

}

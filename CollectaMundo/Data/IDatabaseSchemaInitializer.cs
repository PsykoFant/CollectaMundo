using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface IDatabaseSchemaInitializer
    {
        Task CreateTablesAsync(SQLiteConnection conn);
    }

}

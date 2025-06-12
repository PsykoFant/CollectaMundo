using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface IDatabaseSchemaRepository
    {
        Task CreateTablesAsync(SQLiteConnection conn);
        Task CreateIndicesAsync(SQLiteConnection conn);
        Task CreateViewsAsync(SQLiteConnection conn, string retailer);
        Task OptimizeAsync(SQLiteConnection conn);
    }

}

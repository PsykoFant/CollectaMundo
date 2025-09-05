using System.Data.SQLite;

namespace CollectaMundo.Data.CardDatabaseManagement
{
    public interface ICardDatabasePreparationRepo
    {
        Task CreateTablesAsync(SQLiteConnection conn);
        Task CreateIndicesAsync(SQLiteConnection conn);
        Task CreateViewsAsync(SQLiteConnection conn, string retailer);
        Task OptimizeAsync(SQLiteConnection conn);

        // Update DB methods
        Task<int> GetNumberOfSetsAsync(SQLiteConnection conn);
        Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress);
        Task DropTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task CopyTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress);
    }

}

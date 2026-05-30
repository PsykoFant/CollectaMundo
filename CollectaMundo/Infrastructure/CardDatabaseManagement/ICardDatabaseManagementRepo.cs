using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public interface ICardDatabaseManagementRepo
    {
        // Create
        Task CreateTablesAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task CreateIndicesAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task CreateViewsAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task OptimizeAsync(SQLiteConnection conn);

        // Update
        Task<int> GetNumberOfSetsAsync(SQLiteConnection conn, CancellationToken ct = default);
        Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress);
        Task DropTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task CopyTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress);

        // Export
        Task<string?> ExportCollectionAsync(SQLiteConnection conn, string backupFolderPath, CancellationToken ct = default);
    }

}

using CollectaMundo.Infrastructure.CollectionExport.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CollectionExport
{
    public interface ICollectionExportRepo
    {
        Task<IReadOnlyList<CollectionTradeExportItem>> GetTradeListItemsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CollectionBackupRow>> GetFullCollectionRowsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default);
    }
}

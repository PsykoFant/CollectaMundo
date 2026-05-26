using CollectaMundo.DomainLogic.Decks.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Decks
{
    public interface IDeckManagementRepo
    {
        Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync(SQLiteConnection conn);
        Task UpsertMetadataAsync(SQLiteConnection conn, int locationId, string? format, string? description);
        Task<int> DeleteMetadataAsync(SQLiteConnection conn, int locationId);
    }
}

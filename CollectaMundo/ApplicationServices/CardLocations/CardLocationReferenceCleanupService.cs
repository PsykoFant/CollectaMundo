using CollectaMundo.Infrastructure.Decks;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationReferenceCleanupService(IDeckManagementRepo deckManagementRepo) : ICardLocationReferenceCleanupService
    {
        private readonly IDeckManagementRepo _deckManagementRepo = deckManagementRepo;
        public Task CleanupBeforeLocationDeleteAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId)
        {
            return _deckManagementRepo.DeleteMetadataAsync(conn, tx, locationId);
        }
    }
}

using CollectaMundo.DomainLogic.Decks.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Decks
{
    public interface IDeckBuilderRepo
    {
        Task<IReadOnlyList<DeckCardEntry>> GetByDeckLocationIdAsync(SQLiteConnection conn, int locationId);
        Task ReplaceDeckAsync(SQLiteConnection connection, SQLiteTransaction transaction, int locationId, IReadOnlyCollection<DeckCardEntry> entries);
    }
}

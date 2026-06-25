using CollectaMundo.DomainLogic.Decks.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Decks
{
    public interface IDeckBuilderRepo
    {
        Task<IReadOnlyList<DeckCardEntry>> GetByDeckLocationIdAsync(SQLiteConnection conn, int locationId);
        Task ReplaceDeckAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId, IReadOnlyList<DeckCardEntry> entries);
    }
}

using CollectaMundo.DomainLogic.CardData.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.CardData
{
    public interface ICardDataRepo
    {
        Task<List<OracleFaceCandidate>> GetOracleFaceCandidatesAsync(SQLiteConnection connection);
        Task RebuildCanonicalOracleFacesAsync(SQLiteConnection connection, SQLiteTransaction transaction, IReadOnlyList<CanonicalOracleFace> faces);
        Task<int> GetCanonicalOracleFaceCountAsync(SQLiteConnection connection, SQLiteTransaction? transaction = null);
    }
}

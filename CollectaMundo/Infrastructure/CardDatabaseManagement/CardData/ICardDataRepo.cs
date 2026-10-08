using CollectaMundo.DomainLogic.CardData.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.CardData
{
    public interface ICardDataRepo
    {
        Task<List<OracleFaceCandidate>> GetOracleFaceCandidatesAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<int> RebuildCanonicalOracleFacesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<CanonicalOracleFace> faces);
    }
}

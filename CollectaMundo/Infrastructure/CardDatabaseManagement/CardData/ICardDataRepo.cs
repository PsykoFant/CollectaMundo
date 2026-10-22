using CollectaMundo.DomainLogic.CardData.Models;
using CollectaMundo.DomainLogic.CardLegalities;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.CardData
{
    public interface ICardDataRepo
    {
        Task<List<OracleFaceCandidate>> GetOracleFaceCandidatesAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<int> RebuildCanonicalOracleFacesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<CanonicalOracleFace> canonicalFaces);
        Task<int> RebuildPrintingCardsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyDictionary<string, CardLegalityMasks> legalityMasksByUuid);
    }
}

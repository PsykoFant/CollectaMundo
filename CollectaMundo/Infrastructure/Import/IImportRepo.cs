using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Data;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Import
{
    public interface IImportRepo
    {
        Task<List<string>> GetCardIdentifierColumns(SQLiteConnection conn);
        Task<Dictionary<string, List<string>>> GetCardUuidsByIdFieldAsync(SQLiteConnection conn, string identifierFieldName, IEnumerable<string> valuesEnumerable);

        // step 3
        Task<Dictionary<string, List<string>>> QueryByNameAndSetCodeAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetCode)> pairs, CancellationToken token);
        Task<Dictionary<string, List<string>>> QueryByNameAndSetNameAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetName)> pairs, CancellationToken token);
        Task<Dictionary<string, List<string>>> QueryByNameOnlyAsync(SQLiteConnection conn, IReadOnlyList<string> names, CancellationToken token);

        // step 9

        // Tier 1: cards/tokens lookup
        Task<IReadOnlyDictionary<string, BaseAvailability>> FetchBaseAvailabilityAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token);

        // Tier 2: foreign languages lookup (subset uuids)
        Task<IReadOnlyDictionary<string, HashSet<string>>> FetchForeignLanguagesAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token);

        Task<IReadOnlyList<MyCollectionRow>> UpsertMyCollectionAsync(IReadOnlyList<CollectionUpsertItem> items, SQLiteConnection conn, SQLiteTransaction tx, IProgress<int>? percent, CancellationToken token);
    }
}

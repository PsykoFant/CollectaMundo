using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Import
{
    public interface IImportRepo
    {
        Task<List<string>> GetCardIdentifierColumns(SQLiteConnection conn);
        Task<Dictionary<string, List<string>>> GetCardUuidsByIdFieldAsync(SQLiteConnection conn, string identifierFieldName, IEnumerable<string> values);
    }
}

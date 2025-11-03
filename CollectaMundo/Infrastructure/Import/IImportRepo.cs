using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Import
{
    public interface IImportRepo
    {
        Task<List<string>> GetCardIdentifierColumns(SQLiteConnection conn);
    }
}

using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.GenerateMissingPng
{
    public interface IGenerateMissingPngRepo
    {
        Task<List<string>> GetUniqueValuesAsync(SQLiteConnection conn, string table, string column);
        Task<List<string>> GetValuesWithNullAsync(SQLiteConnection conn, string table, string returnColumn, string nullColumn);
        Task InsertIfNotExistsAsync(SQLiteConnection conn, string table, string column, string value);
        Task<bool> UpdateImageAsync(SQLiteConnection conn, string tableName, string imageColumn, string referenceColumn, string referenceValue, byte[] imageData);
        Task<bool> UpdateKeyruneImageAsync(SQLiteConnection conn, string setCode, byte[] imageData, bool usedDefaultSvg);
        Task<Dictionary<string, byte[]>> GetManaSymbolImagesAsync(SQLiteConnection conn, IEnumerable<string> symbols);
        Task InsertMissingFromColumnAsync(SQLiteConnection conn, string fromTable, string fromColumn, string intoTable, string intoColumn);
        Task DeleteWhereDefaultSvgUsedAsync(SQLiteConnection conn);
    }
}

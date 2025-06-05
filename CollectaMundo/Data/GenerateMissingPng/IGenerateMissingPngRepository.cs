using System.Data.SQLite;

namespace CollectaMundo.Data.GenerateMissingPng
{
    public interface IGenerateMissingPngRepository
    {
        Task<List<string>> GetUniqueValuesAsync(SQLiteConnection conn, string table, string column);
        Task<List<string>> GetValuesWithNullAsync(SQLiteConnection conn, string table, string returnColumn, string nullColumn);
        Task InsertIfNotExistsAsync(SQLiteConnection conn, string table, string column, string value);
        Task UpdateImageAsync(SQLiteConnection conn, string table, string imageColumn, string keyColumn, string keyValue, byte[] imageData);
    }

}

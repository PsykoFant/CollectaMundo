using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public interface IGenerateMissingPngService
    {
        Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn);
        Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn);
        Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn);
    }
}

using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public interface IGenerateMissingPngService
    {
        Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn, IProgress<int> percentProgress);
        Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, IProgress<int> percentProgress);
        Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, IProgress<int> percentProgress);
    }
}

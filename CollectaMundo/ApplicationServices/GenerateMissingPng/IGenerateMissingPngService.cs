using CollectaMundo.ViewModels;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public interface IGenerateMissingPngService
    {
        Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn, StatusViewModel statusVm);
        Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, StatusViewModel statusVm);
        Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, StatusViewModel statusVm);
    }

}

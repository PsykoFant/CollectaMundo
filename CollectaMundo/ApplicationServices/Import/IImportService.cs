using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        string? PromptForCsvFile();
        Task<ColumnMapping> LoadCsvFileAsync(string filePath);
    }
}

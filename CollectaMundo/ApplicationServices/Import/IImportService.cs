using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        string? PromptForCsvFile();
        Task<(List<TempCardItem> parsedItems, ColumnMapping mapping)> LoadCsvFileAsync(string filePath);
        Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, ColumnMapping mapping);
    }
}

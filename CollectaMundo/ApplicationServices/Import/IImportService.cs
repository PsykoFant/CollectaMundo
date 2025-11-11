using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        string? PromptForCsvFile();
        Task<(List<TempCardItem>, ColumnMapping)> LoadCsvFileAsync(string filePath, ProgressSinks? progress = null);
        Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, ColumnMapping mapping);
    }
}

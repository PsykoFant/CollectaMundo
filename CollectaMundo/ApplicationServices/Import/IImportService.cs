using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        // Step 1
        string? PromptForCsvFile();
        Task<(List<TempCardItem>, ColumnMapping)> LoadCsvFileAsync(string filePath, ProgressSinks progress, CancellationToken cancelToken);

        // Step 2
        Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, ColumnMapping mapping, ProgressSinks progress, CancellationToken cancelToken);
    }
}

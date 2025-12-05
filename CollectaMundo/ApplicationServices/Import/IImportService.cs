using System.Collections.ObjectModel;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        // Step 1
        string? PromptForCsvFile();
        Task<(List<TempCardItem>, IdColumnMapping)> LoadCsvFileAsync(string filePath, ProgressSinks progress, CancellationToken cancelToken);

        // Step 2
        Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, IdColumnMapping mapping, ProgressSinks progress, CancellationToken cancelToken);

        // Step 3
        Task<ImportMatchSummaryDto> TryResolveUuidsFromNameAndSetAsync(IReadOnlyList<TempCardItem> importCandidates, IReadOnlyList<NameSetColumnMapping> mappings, ProgressSinks progress, CancellationToken token);

        // Step 4
        ImportMatchSummaryDto ApplyUserSelectedUuids(ObservableCollection<TempCardItem> importCandidates, List<MultipleUuidsItem> userSelections, ProgressSinks progress);
    }
}

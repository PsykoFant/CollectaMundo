using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Models;
using System.Collections.ObjectModel;

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
        Task<ImportMatchSummaryDto> TryResolveUuidsFromNameAndSetAsync(IReadOnlyList<TempCardItem> importCandidates, IReadOnlyList<CsvFieldMapping> mappings, ProgressSinks progress, CancellationToken token);

        // Step 4
        ImportMatchSummaryDto ApplyUserSelectedUuids(ObservableCollection<TempCardItem> importCandidates, List<MultipleUuidsItem> userSelections, ProgressSinks progress);

        // Step 5
        Task<List<string>> GetAvailableFinishesAsync();
        Task<List<string>> GetAvailableLanguagesAsync();
        Task<List<string>> GetAvailableLocationsAsync();

        // Step 9
        Task<IReadOnlyList<ResolvedImportItem>> ResolveImportItemsStrictAsync(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> additionalMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings, IReadOnlyList<CsvValueMapping> locationMappings, CancellationToken token);
        ImportSummary BuildImportSummary(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> tempItems, IReadOnlyList<CsvFieldMapping> nameSetMappings, IReadOnlyList<CsvFieldMapping> additionalFieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings, IReadOnlyList<CsvValueMapping> locationMappings);
        Task<OperationResult> SaveUnimportableItemsAsync(ImportSummary summary, IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> importItems);
        Task<ImportExecutionResult> ImportResolvedItems(IReadOnlyList<ResolvedImportItem> resolvedItems, ProgressSinks progress, CancellationToken token);

    }
}

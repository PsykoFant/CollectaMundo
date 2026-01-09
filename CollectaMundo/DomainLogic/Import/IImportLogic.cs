using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.ViewModels.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.DomainLogic.Import
{
    public interface IImportLogic
    {
        // Step 1
        Task<List<TempCardItem>> ParseCsvFileAsync(string filePath, IProgress<int> progress, CancellationToken cancelToken);
        // Step 2
        ImportMatchSummaryDto AssignUuidsToImportItems(List<TempCardItem> importCandidates, Dictionary<string, List<string>> idToUuids, string selectedCsvHeader, IProgress<int>? percentProgress, CancellationToken cancelToken);

        // Step 3
        (bool HasName, bool HasSetName, bool HasSetCode, string? NameHeader, string? SetNameHeader, string? SetCodeHeader) ExtractMappedFields(IReadOnlyList<CsvFieldMapping> mappings);
        bool IsItemResolved(TempCardItem item);
        void ApplySetCodeMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetCode)> pairs, Dictionary<string, List<string>> results);
        void ApplySetNameMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetName)> pairs, Dictionary<string, List<string>> results);
        void ApplyNameOnlyMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<string> names, Dictionary<string, List<string>> results);
        ImportMatchSummaryDto FinalizeMatchResults(IReadOnlyList<TempCardItem> items);

        // Step 4
        ImportMatchSummaryDto ApplySelectedUuids(ObservableCollection<TempCardItem> importCandidates, List<MultipleUuidsItem> userSelections);

        // Step 9
        IReadOnlyList<ResolvedImportItem> ResolveImportItems(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> fieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings);
        ImportSummary BuildImportSummary(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> tempItems, IReadOnlyList<CsvFieldMapping> nameSetMappings, IReadOnlyList<CsvFieldMapping> additionalFieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings);
        string BuildUnimportableItemsCsv(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> importItems);
        public IReadOnlyList<CollectionUpsertItem> CollapseResolvedItemsForCollection(IReadOnlyList<ResolvedImportItem> resolvedItems);
    }
}

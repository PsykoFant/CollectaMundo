using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.DomainLogic.Import
{
    public interface IImportLogic
    {
        // Step 1
        Task<List<TempCardItem>> ParseCsvFileAsync(string filePath, IProgress<int> progress, CancellationToken cancelToken);
        // Step 2
        ImportMatchSummaryDto AssignUuidsToImportItems(List<TempCardItem> importCandidates, Dictionary<string, List<string>> idToUuids, string selectedCsvHeader, IProgress<int>? percentProgress, CancellationToken cancelToken);

        // Step 3
        (bool HasName, bool HasSetName, bool HasSetCode, string? NameHeader, string? SetNameHeader, string? SetCodeHeader) ExtractMappedFields(IReadOnlyList<NameSetColumnMapping> mappings);
        bool IsItemResolved(TempCardItem item);
        void ApplySetCodeMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetCode)> pairs, Dictionary<string, List<string>> results);
        void ApplySetNameMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetName)> pairs, Dictionary<string, List<string>> results);
        void ApplyNameOnlyMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<string> names, Dictionary<string, List<string>> results);
        ImportMatchSummaryDto FinalizeMatchResults(IReadOnlyList<TempCardItem> items);
    }
}

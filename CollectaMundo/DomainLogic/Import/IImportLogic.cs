using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.DomainLogic.Import
{
    public interface IImportLogic
    {
        Task<List<TempCardItem>> ParseCsvFileAsync(string filePath, IProgress<int> progress, CancellationToken cancelToken);
        ImportMatchSummaryDto AssignUuidsToImportItems(List<TempCardItem> importCandidates, Dictionary<string, List<string>> idToUuids, string selectedCsvHeader);
    }
}

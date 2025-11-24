using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Import
{
    public class ImportService(IDbConnectionFactory dbFactory, IImportRepo importRepo, IFileSystemPicker fileSystemPicker, IImportLogic importLogic) : IImportService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IImportRepo _importRepo = importRepo;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;
        private readonly IImportLogic _importLogic = importLogic;

        public string? PromptForCsvFile()
        {
            var file = _fileSystemPicker.PickFile("Select your CSV file to import");
            return file;
        }

        // Step 1
        public async Task<(List<TempCardItem>, IdColumnMapping)> LoadCsvFileAsync(string filePath, ProgressSinks progress, CancellationToken cancelToken)
        {
            try
            {
                cancelToken.ThrowIfCancellationRequested(); // Fast exit if cancelled before start               

                progress.ProgressBarVisible.Report(true);
                progress.Percent.Report(0);

                // Calls ParseCsvFileAsync with progress reporter
                var parsedItems = await _importLogic.ParseCsvFileAsync(filePath, progress.Percent, cancelToken);

                var csvHeaders = parsedItems.FirstOrDefault()?.Fields.Keys.ToList() ?? [];
                var dbFields = await CardIdentifiersColumns();

                var mapping = new IdColumnMapping
                {
                    CsvHeaders = csvHeaders,
                    DatabaseFields = dbFields,
                    SelectedCsvHeader = csvHeaders.FirstOrDefault(),
                    SelectedDatabaseField = dbFields.FirstOrDefault()
                };

                progress.Percent.Report(100);
                progress.ProgressBarVisible.Report(false);

                return (parsedItems, mapping);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log or handle other exceptions as needed
                throw new ApplicationException("An error occurred while parsin the CSV file.", ex);
            }

        }
        private async Task<List<string>> CardIdentifiersColumns()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();
            try
            {
                var result = await _importRepo.GetCardIdentifierColumns(uow.CurrentConnection);
                await uow.CommitAsync();
                return result;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                await uow.DisposeAsync();
            }
        }

        // Step 2
        public async Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, IdColumnMapping mapping, ProgressSinks progress, CancellationToken cancelToken)
        {
            var lookupValues = importCandidates.Select(item => item.Fields.TryGetValue(mapping.SelectedCsvHeader!, out var val) ? val : null)
                .Where(val => !string.IsNullOrWhiteSpace(val))
                .Select(val => val!) // safely assert non-null
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var idToUuids = await GetCardUuidsByIdFieldAsync(mapping.SelectedDatabaseField!, lookupValues);

            var summary = _importLogic.AssignUuidsToImportItems(importCandidates, idToUuids, mapping.SelectedCsvHeader!, progress.Percent, cancelToken);

            Debug.WriteLine("[TryResolveUuidsFromMappedIdAsync] Import id match done - returning ... ");
            return summary;
        }
        private async Task<Dictionary<string, List<string>>> GetCardUuidsByIdFieldAsync(string identifierFieldName, IEnumerable<string> values)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();
            try
            {
                var result = await _importRepo.GetCardUuidsByIdFieldAsync(uow.CurrentConnection, identifierFieldName, values);
                await uow.CommitAsync();
                return result;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                await uow.DisposeAsync();
            }
        }

        // Step 3
        public async Task<ImportMatchSummaryDto> TryResolveUuidsFromNameAndSetAsync(
    IReadOnlyList<TempCardItem> importCandidates,
    IReadOnlyList<NameSetColumnMapping> mappings,
    ProgressSinks progress,
    CancellationToken token)
        {
            if (importCandidates.Count == 0)
            {
                return new ImportMatchSummaryDto();
            }

            // Extract final mapping selection
            var chosen = _importLogic.ExtractMappedFields(mappings);

            if (!chosen.HasName)
            {
                // UI should prevent this, but domain must protect itself
                throw new InvalidOperationException("Card Name must be mapped.");
            }

            // Obtain headers (may be null)
            string nameHeader = chosen.NameHeader!;
            string? setCodeHeader = chosen.SetCodeHeader;
            string? setNameHeader = chosen.SetNameHeader;

            // Batch all items (Step 3 always operates on full import list)
            var items = importCandidates.ToList();

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            try
            {
                // ------------------------------
                // 1) NAME + SET CODE
                // ------------------------------
                if (chosen.HasSetCode)
                {
                    var pairs = ExtractPairs(items, nameHeader, setCodeHeader!);

                    var results = await _importRepo.QueryByNameAndSetCodeAsync(
                        uow.CurrentConnection,
                        pairs,
                        token);

                    _importLogic.ApplySetCodeMatches(items, pairs, results);
                }

                // ------------------------------
                // 2) NAME + SET NAME
                // ------------------------------
                if (chosen.HasSetName)
                {
                    var pairs = ExtractPairs(items, nameHeader, setNameHeader!);

                    var results = await _importRepo.QueryByNameAndSetNameAsync(
                        uow.CurrentConnection,
                        pairs,
                        token);

                    _importLogic.ApplySetNameMatches(items, pairs, results);
                }

                // ------------------------------
                // 3) NAME-ONLY FALLBACK
                //
                // Only triggered when:
                // - SetCode NOT mapped
                // - SetName NOT mapped
                // ------------------------------
                if (!chosen.HasSetCode && !chosen.HasSetName)
                {
                    // Collect only the card-name values (aligned by item index)
                    var names = items
                        .Select(i => i.Fields.TryGetValue(nameHeader, out var n) ? n : "")
                        .ToList();

                    var results = await _importRepo.QueryByNameOnlyAsync(
                        uow.CurrentConnection,
                        names,
                        token);

                    _importLogic.ApplyNameOnlyMatches(items, names, results);
                }

                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }

            // ------------------------------
            // 4) Final classification
            // ------------------------------
            var summary = _importLogic.FinalizeMatchResults(items);

            return summary;
        }

        private static List<(string Name, string Value)> ExtractPairs(List<TempCardItem> items, string nameHeader, string otherHeader)
        {
            return [.. items.Select(i => (
                    i.Fields.TryGetValue(nameHeader, out var name) ? name : "",
                    i.Fields.TryGetValue(otherHeader, out var val) ? val : ""
                ))];
        }
    }
}

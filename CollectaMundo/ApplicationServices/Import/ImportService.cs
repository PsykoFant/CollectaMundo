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
        public async Task<ImportMatchSummaryDto> TryResolveUuidsFromNameAndSetAsync(IReadOnlyList<TempCardItem> importCandidates, IReadOnlyList<NameSetColumnMapping> mappings, ProgressSinks progress, CancellationToken token)
        {

            int total = importCandidates.Count;
            int processed = 0;

            progress.Percent.Report(0); // start


            // Extract chosen mapping fields (domain logic)
            var (HasName, HasSetName, HasSetCode, NameHeader, SetNameHeader, SetCodeHeader) = _importLogic.ExtractMappedFields(mappings);

            if (!HasName || (!HasSetName && !HasSetCode))
            {
                throw new InvalidOperationException("Name and either Set Name or Set Code must be mapped.");
            }

            const int BatchSize = 800;

            // Start read-only UoW (because Step 3 is SELECT-only)
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            try
            {
                for (int start = 0; start < importCandidates.Count; start += BatchSize)
                {
                    token.ThrowIfCancellationRequested();

                    // Extract batch of unresolved items
                    var batch = importCandidates
                        .Skip(start)
                        .Take(BatchSize)
                        .Where(i => !_importLogic.IsItemResolved(i))
                        .ToList();

                    if (batch.Count == 0)
                    {
                        continue;
                    }

                    processed += batch.Count;

                    int percent = (int)((double)processed / total * 100);
                    progress.Percent.Report(percent);

                    progress.Detail.Report($"Processing batch {start / BatchSize + 1}...");

                    //  Match by SET CODE
                    if (HasSetCode)
                    {
                        var pairs = ExtractPairs(batch, NameHeader!, SetCodeHeader!);

                        var results = await _importRepo.QueryByNameAndSetCodeAsync(
                            uow.CurrentConnection,
                            pairs,
                            token);

                        _importLogic.ApplySetCodeMatches(batch, pairs, results);
                    }

                    //  Match by SET NAME
                    if (HasSetName)
                    {
                        var pairs = ExtractPairs(batch, NameHeader!, SetNameHeader!);

                        var results = await _importRepo.QueryByNameAndSetNameAsync(
                            uow.CurrentConnection,
                            pairs,
                            token);

                        _importLogic.ApplySetNameMatches(batch, pairs, results);
                    }
                }

                await uow.CommitAsync();

                // Final domain-level integrity & outcome evaluation
                return _importLogic.FinalizeMatchResults(importCandidates);
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
        private static List<(string Name, string Value)> ExtractPairs(List<TempCardItem> items, string nameHeader, string otherHeader)
        {
            return [.. items.Select(i => (
                    i.Fields.TryGetValue(nameHeader, out var name) ? name : "",
                    i.Fields.TryGetValue(otherHeader, out var val) ? val : ""
                ))];
        }
    }
}

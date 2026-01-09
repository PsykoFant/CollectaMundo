using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.ViewModels.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

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

                var csvHeaders = parsedItems.FirstOrDefault()?.CsvFields.Keys.ToList() ?? [];
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
            var lookupValues = importCandidates.Select(item => item.CsvFields.TryGetValue(mapping.SelectedCsvHeader!, out var val) ? val : null)
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
        public async Task<ImportMatchSummaryDto> TryResolveUuidsFromNameAndSetAsync(IReadOnlyList<TempCardItem> importCandidates, IReadOnlyList<CsvFieldMapping> mappings, ProgressSinks progress, CancellationToken token)
        {
            var (HasName, HasSetName, HasSetCode, NameHeader, SetNameHeader, SetCodeHeader) = _importLogic.ExtractMappedFields(mappings);

            if (!HasName)
            {
                throw new InvalidOperationException("Card Name must be mapped.");
            }

            const int BatchSize = 800;
            int total = importCandidates.Count;
            int processed = 0;

            progress.Percent.Report(0);

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            try
            {
                for (int start = 0; start < importCandidates.Count; start += BatchSize)
                {
                    token.ThrowIfCancellationRequested();

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

                    // Try (Name + SetCode) if mapped
                    if (HasSetCode)
                    {
                        var pairs = ExtractPairs(batch, NameHeader!, SetCodeHeader!);
                        var results = await _importRepo.QueryByNameAndSetCodeAsync(uow.CurrentConnection, pairs, token);
                        _importLogic.ApplySetCodeMatches(batch, pairs, results);
                    }

                    // Try (Name + SetName) if mapped and item still unresolved
                    if (HasSetName)
                    {
                        var unresolved = batch.Where(i => !_importLogic.IsItemResolved(i)).ToList();
                        if (unresolved.Count > 0)
                        {
                            var pairs = ExtractPairs(unresolved, NameHeader!, SetNameHeader!);
                            var results = await _importRepo.QueryByNameAndSetNameAsync(uow.CurrentConnection, pairs, token);
                            _importLogic.ApplySetNameMatches(unresolved, pairs, results);
                        }
                    }

                    // Fallback: Name-only for items still unresolved
                    {
                        var unresolved = batch.Where(i => !_importLogic.IsItemResolved(i)).ToList();
                        if (unresolved.Count > 0)
                        {
                            var names = unresolved.Select(i => i.CsvFields.TryGetValue(NameHeader!, out var v) ? v : string.Empty).ToList();
                            var results = await _importRepo.QueryByNameOnlyAsync(uow.CurrentConnection, names, token);
                            _importLogic.ApplyNameOnlyMatches(unresolved, names, results);
                        }
                    }
                }

                await uow.CommitAsync();
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
            i.CsvFields.TryGetValue(nameHeader, out var name) ? name : string.Empty,
            i.CsvFields.TryGetValue(otherHeader, out var val) ? val : string.Empty
            ))];
        }

        // Step 4
        public ImportMatchSummaryDto ApplyUserSelectedUuids(ObservableCollection<TempCardItem> importCandidates, List<MultipleUuidsItem> userSelections, ProgressSinks progress)
        {
            return _importLogic.ApplySelectedUuids(importCandidates, userSelections);
        }

        // Step 5
        public async Task<List<string>> GetAvailableFinishesAsync()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            try
            {
                var rawValues = await DbHelpers.GetUniqueValuesAsync(
                    uow.CurrentConnection,
                    "cards",
                    "finishes");

                await uow.CommitAsync();

                return ImportValueNormalizer.SplitAndDistinct(rawValues);
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
        public async Task<List<string>> GetAvailableLanguagesAsync()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            try
            {
                var rawValues = await DbHelpers.GetUniqueValuesAsync(
                    uow.CurrentConnection,
                    "cardForeignData",
                    "language");

                await uow.CommitAsync();

                return ImportValueNormalizer.SplitAndDistinct(rawValues);
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        // Step 9
        public IReadOnlyList<ResolvedImportItem> ResolveImportItems(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> additionalMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings)
        {
            return _importLogic.ResolveImportItems(items, additionalMappings, conditionMappings, finishMappings, languageMappings);
        }
        public ImportSummary BuildImportSummary(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> tempItems, IReadOnlyList<CsvFieldMapping> nameSetMappings, IReadOnlyList<CsvFieldMapping> additionalFieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings)
        {
            return _importLogic.BuildImportSummary(resolvedItems, tempItems, nameSetMappings, additionalFieldMappings, conditionMappings, finishMappings, languageMappings);
        }
        public async Task<OperationResult> SaveUnimportableItemsAsync(ImportSummary summary, IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> importItems)
        {
            // Guard: nothing to save
            if (summary.UnableToImportCount == 0)
            {
                return new OperationResult(
                    OperationResultCode.Success,
                    "No unimportable items to save.");
            }

            // Suggest a default filename (user can change both name and location)
            var defaultFileName = $"unimportable-items-{DateTime.Now:yyyyMMdd-HHmmss}.csv";

            // Ask user where and under what name to save
            var filePath = _fileSystemPicker.PickSaveFile(title: "Save unimportable items", defaultFileName: defaultFileName, filter: "CSV Files (*.csv)|*.csv");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new OperationResult(
                    OperationResultCode.NoOp,
                    "User cancelled save dialog.");
            }

            // Build CSV contents using FINAL importability result
            var content = _importLogic.BuildUnimportableItemsCsv(
                resolvedItems,
                importItems);

            // Write file using UTF-8 with BOM (Excel-compatible)
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            await File.WriteAllTextAsync(filePath, content, utf8WithBom);

            return new OperationResult(
                OperationResultCode.Success,
                $"Saved unimportable items to {filePath}");
        }
        public async Task<OperationResult> ImportResolvedItems(IReadOnlyList<ResolvedImportItem> resolvedItems, ProgressSinks progress, CancellationToken token)
        {
            if (resolvedItems == null || resolvedItems.Count == 0)
            {
                return new(OperationResultCode.Empty, "No resolved items to import.");
            }

            progress.Detail.Report("Preparing import items...");

            var collapsed = _importLogic.CollapseResolvedItemsForCollection(resolvedItems);

            if (collapsed.Count == 0)
            {
                return new(OperationResultCode.Success, "No importable items found.");
            }

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync(); // write transaction

            try
            {
                token.ThrowIfCancellationRequested();

                progress.Detail.Report("Importing cards to collection...");
                progress.Percent.Report(0);

                await _importRepo.UpsertMyCollectionAsync(collapsed, uow.CurrentConnection, uow.CurrentTransaction, progress.Percent, token);


                await uow.CommitAsync();

                progress.Detail.Report("Import completed.");
                return new OperationResult(
                    OperationResultCode.Success,
                    $"Finished importing {collapsed.Count} unique collection rows.");
            }
            catch (OperationCanceledException)
            {
                await uow.RollbackAsync();
                return new(OperationResultCode.CancelledByUser, "Import cancelled by user.");
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new(OperationResultCode.Error, $"Import failed: {ex.Message}");
            }
        }




    }
}

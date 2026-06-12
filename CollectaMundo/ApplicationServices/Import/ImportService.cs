using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.ViewModels.Models;
using ServiceStack;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollectaMundo.ApplicationServices.Import
{
    public class ImportService(IUnitOfWorkRunner uowRunner, IImportRepo importRepo, IFileSystemPicker fileSystemPicker, IImportLogic importLogic, ICardLocationService cardLocationService) : IImportService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IImportRepo _importRepo = importRepo;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;
        private readonly IImportLogic _importLogic = importLogic;
        private readonly ICardLocationService _cardLocationService = cardLocationService;
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
            return await _uowRunner.ExecuteReadOnlyAsync(conn => _importRepo.GetCardIdentifierColumns(conn));
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
            return await _uowRunner.ExecuteReadOnlyAsync(conn => _importRepo.GetCardUuidsByIdFieldAsync(conn, identifierFieldName, values));
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

            return await _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                for (int start = 0; start < importCandidates.Count; start += BatchSize)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = importCandidates.Skip(start).Take(BatchSize).Where(i => !_importLogic.IsItemResolved(i)).ToList();

                    Debug.WriteLine($"[TryResolveUuidsFromNameAndSetAsync] Processing batch {start / BatchSize + 1} with {batch.Count} items ... ");

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
                        var results = await _importRepo.QueryByNameAndSetCodeAsync(conn, pairs, token);
                        _importLogic.ApplySetCodeMatches(batch, pairs, results);
                    }

                    // Try (Name + SetName) if mapped and item still unresolved
                    if (HasSetName)
                    {
                        var unresolved = batch.Where(i => !_importLogic.IsItemResolved(i)).ToList();
                        if (unresolved.Count > 0)
                        {
                            var pairs = ExtractPairs(unresolved, NameHeader!, SetNameHeader!);
                            var results = await _importRepo.QueryByNameAndSetNameAsync(conn, pairs, token);
                            _importLogic.ApplySetNameMatches(unresolved, pairs, results);
                        }
                    }

                    // Fallback: Name-only for items still unresolved
                    {
                        var unresolved = batch.Where(i => !_importLogic.IsItemResolved(i)).ToList();
                        if (unresolved.Count > 0)
                        {
                            var names = unresolved.Select(i => i.CsvFields.TryGetValue(NameHeader!, out var v) ? v : string.Empty).ToList();
                            var results = await _importRepo.QueryByNameOnlyAsync(conn, names, token);
                            _importLogic.ApplyNameOnlyMatches(unresolved, names, results);
                        }
                    }
                }

                return _importLogic.FinalizeMatchResults(importCandidates);
            });
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
            var rawValues = await _uowRunner.ExecuteReadOnlyAsync(conn => DbHelpers.GetUniqueValuesAsync(conn, "cards", "finishes"));
            return ImportValueNormalizer.SplitAndDistinct(rawValues);
        }
        public async Task<List<string>> GetAvailableLanguagesAsync()
        {
            var rawValues = await _uowRunner.ExecuteReadOnlyAsync(conn => DbHelpers.GetUniqueValuesAsync(conn, "cardForeignData", "language"));
            return ImportValueNormalizer.SplitAndDistinct(rawValues);
        }
        public async Task<List<string>> GetAvailableLocationsAsync()
        {
            var locations = await _cardLocationService.GetAllLocationsAsync();

            return
            [
                .. locations
                .Select(x => x.Name)
            ];
        }

        // Step 10: resolve + strict validate via DB
        public async Task<IReadOnlyList<ResolvedImportItem>> ResolveImportItemsStrictAsync(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> additionalMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings, IReadOnlyList<CsvValueMapping> locationMappings, bool createMissingLocationsAsStorage, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var availableLocations = await _cardLocationService.GetAllLocationsAsync();

            if (createMissingLocationsAsStorage)
            {
                try
                {
                    await CreateMissingLocationsAsStorageAsync(locationMappings, token);

                    token.ThrowIfCancellationRequested();

                    availableLocations = await _cardLocationService.GetAllLocationsAsync();

                    AutoMapNewlyCreatedLocations(locationMappings, availableLocations);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Failed to create missing card locations during import.",
                        ex);
                }
            }

            var resolved = _importLogic.ResolveImportItems(items, additionalMappings, conditionMappings, finishMappings, languageMappings, locationMappings, availableLocations);

            token.ThrowIfCancellationRequested();

            // 2) Collect UUIDs we need to validate (only importable candidates)
            var uuidsToValidate = CollectUuidsToValidate(resolved);

            // No UUIDs => nothing to validate; return as-is
            if (uuidsToValidate.Count == 0)
            {
                return resolved;
            }

            // 3) Determine whether we need foreign languages (Tier 2)
            // Only needed when any importable item requests non-English language.
            var needsForeign = CollectUuidsNeedingForeignLanguageLookup(resolved);

            // 3a) If we have only English requests, we still need Tier 1 (base language + finishes)
            // to validate "English" and finish availability.
            // So we proceed with Tier 1 regardless.

            return await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                token.ThrowIfCancellationRequested();

                // 4) Tier 1: base availability for ALL uuids (cards/tokens)
                var baseByUuid = await _importRepo.FetchBaseAvailabilityAsync(uuidsToValidate, conn, tx, token);

                token.ThrowIfCancellationRequested();

                // 5) Tier 2: foreign languages only for non-English requested uuids
                IReadOnlyDictionary<string, HashSet<string>> foreignByUuid = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                if (needsForeign.Count > 0)
                {
                    foreignByUuid = await _importRepo.FetchForeignLanguagesAsync(needsForeign, conn, tx, token);

                    token.ThrowIfCancellationRequested();
                }

                var index = new AvailabilityIndex
                {
                    BaseByUuid = baseByUuid,
                    ForeignLanguagesByUuid = foreignByUuid
                };

                // 6) Strict validation in DomainLogic (marks unimportable + warnings)
                _importLogic.ApplyStrictVariantValidation(resolved, index);

                return (Result: resolved, Commit: true);
            });
        }

        // Helpers for ResolveImportItemsStrictAsync
        private async Task CreateMissingLocationsAsStorageAsync(IReadOnlyList<CsvValueMapping> locationMappings, CancellationToken token)
        {
            var missingLocationNames = locationMappings
                .Where(m => string.IsNullOrWhiteSpace(m.SelectedCardSetValue))
                .Select(m => m.CsvValue?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingLocationNames.Count == 0)
            {
                return;
            }

            await _cardLocationService.CreateMissingLocationsAsStorageAsync(missingLocationNames!, token);
        }
        private static void AutoMapNewlyCreatedLocations(IReadOnlyList<CsvValueMapping> locationMappings, IReadOnlyList<CardLocation> availableLocations)
        {
            foreach (var mapping in locationMappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.SelectedCardSetValue))
                {
                    continue;
                }

                var match = availableLocations.FirstOrDefault(x => string.Equals(x.Name, mapping.CsvValue, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    mapping.SelectedCardSetValue = match.Name;
                }
            }
        }
        private static HashSet<string> CollectUuidsToValidate(IReadOnlyList<ResolvedImportItem> resolved)
        {
            // Capacity hint: worst-case all are importable with UUID
            var set = new HashSet<string>(capacity: resolved.Count, comparer: StringComparer.OrdinalIgnoreCase);

            foreach (var r in resolved)
            {
                if (!r.IsImportable)
                {
                    continue;
                }

                var uuid = r.Uuid;
                if (!string.IsNullOrWhiteSpace(uuid))
                {
                    set.Add(uuid);
                }
            }

            return set;
        }
        private static HashSet<string> CollectUuidsNeedingForeignLanguageLookup(IReadOnlyList<ResolvedImportItem> resolved)
        {
            // Only uuids for importable rows requesting non-English.
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in resolved)
            {
                if (!r.IsImportable)
                {
                    continue;
                }

                var uuid = r.Uuid;
                if (string.IsNullOrWhiteSpace(uuid))
                {
                    continue;
                }

                var lang = r.Language;

                // Tier 2 only when requested language != English
                if (!string.Equals(lang, "English", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(uuid);
                }
            }

            return set;
        }

        // Step 10b: build summary + offer to export unimportable items
        public ImportSummary BuildImportSummary(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> tempItems, IReadOnlyList<CsvFieldMapping> nameSetMappings, IReadOnlyList<CsvFieldMapping> additionalFieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings, IReadOnlyList<CsvValueMapping> locationMappings)
        {
            return _importLogic.BuildImportSummary(resolvedItems, tempItems, nameSetMappings, additionalFieldMappings, conditionMappings, finishMappings, languageMappings, locationMappings);
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

            // CreateCollectionChangeSetFromEdits CSV contents using FINAL importability result
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

        // Step 10 --> 11: final import of strictly validated items
        public async Task<ImportExecutionResult> FinalImportResolvedItems(IReadOnlyList<ResolvedImportItem> resolvedItems, ProgressSinks progress, CancellationToken token)
        {
            if (resolvedItems == null || resolvedItems.Count == 0)
            {
                return new ImportExecutionResult(new OperationResult(OperationResultCode.Empty, "No resolved items to import."), Mutation: null);
            }

            progress.Detail.Report("Preparing import items...");

            var collapsed = _importLogic.CollapseResolvedItemsForCollection(resolvedItems);

            if (collapsed.Count == 0)
            {
                return new ImportExecutionResult(new OperationResult(OperationResultCode.Success, "No importable items found."), Mutation: null);
            }

            try
            {
                var upsertedRows = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    token.ThrowIfCancellationRequested();

                    progress.Detail.Report("Importing cards to collection...");
                    progress.Percent.Report(0);

                    Debug.WriteLine("[FinalImportResolvedItems] Upserting collapsed items ... ");

                    // Capture returned rows WITH CardId
                    var upsertedRows = await _importRepo.UpsertMyCollectionAsync(collapsed, conn, tx, progress.Percent, token);

                    return (Result: upsertedRows, Commit: true);
                });

                progress.Detail.Report("Import completed.");

                // build mutation from REAL rows
                var mutation = new ImportCollectionUpsertResult
                {
                    // Fully resolved rows (CardId + Identity + totals)
                    UpsertedRows = upsertedRows
                };

                Debug.WriteLine("[FinalImportResolvedItems] Finished upserting collapsed items ... ");

                return new ImportExecutionResult(new OperationResult(OperationResultCode.Success, $"Finished importing {upsertedRows.Count} unique collection rows."), Mutation: mutation);
            }
            catch (OperationCanceledException)
            {
                return new ImportExecutionResult(new OperationResult(OperationResultCode.CancelledByUser, "Import cancelled by user."), Mutation: null);
            }
            catch (Exception ex)
            {
                return new ImportExecutionResult(new OperationResult(OperationResultCode.Error, $"Import failed: {ex.Message}"), Mutation: null);
            }
        }

    }
}

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
            var (HasName, HasSetName, HasSetCode, NameHeader, SetNameHeader, SetCodeHeader) = _importLogic.ExtractMappedFields(mappings);

            // You still enforce UI validation before calling this
            if (!HasName)
            {
                throw new InvalidOperationException("Card Name must be mapped.");
            }
            // (we do not enforce SetCode/SetName here — fallback will handle it)

            // ---- PREPARE INPUTS ----
            var names = importCandidates.Select(i =>
                (i.Fields.TryGetValue(NameHeader!, out var v) ? v : "")).ToList();

            var nameCodePairs = HasSetCode
                ? importCandidates.Select(i =>
                    (
                        i.Fields.TryGetValue(NameHeader!, out var name) ? name : "",
                        i.Fields.TryGetValue(SetCodeHeader!, out var code) ? code : ""
                    )
                  ).ToList()
                : [];

            var nameSetPairs = HasSetName
                ? importCandidates.Select(i =>
                    (
                        i.Fields.TryGetValue(NameHeader!, out var name) ? name : "",
                        i.Fields.TryGetValue(SetNameHeader!, out var setName) ? setName : ""
                    )
                  ).ToList()
                : [];

            // ---- DATABASE QUERIES ----
            Dictionary<string, List<string>> codeMatches = [];
            Dictionary<string, List<string>> nameMatches = [];
            Dictionary<string, List<string>> setMatches = [];

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            if (HasSetCode)
            {
                codeMatches = await _importRepo.QueryByNameAndSetCodeAsync(
                    uow.CurrentConnection,
                    nameCodePairs,
                    token
                );
            }

            if (HasSetName)
            {
                setMatches = await _importRepo.QueryByNameAndSetNameAsync(
                    uow.CurrentConnection,
                    nameSetPairs,
                    token
                );
            }

            // Always fetch name-only candidates — needed for fallback
            nameMatches = await _importRepo.QueryByNameOnlyAsync(
                uow.CurrentConnection,
                names,
                token
            );

            await uow.CommitAsync();

            // ---- APPLY MATCHES IN PRIORITY ORDER ----

            // 1) Try (Name + SetCode)
            if (HasSetCode)
            {
                _importLogic.ApplySetCodeMatches(importCandidates, nameCodePairs, codeMatches);
            }

            // 2) Try (Name + SetName)
            if (HasSetName)
            {
                _importLogic.ApplySetNameMatches(importCandidates, nameSetPairs, setMatches);
            }

            // 3) Fallback → Name Only
            _importLogic.ApplyNameOnlyMatches(importCandidates, names, nameMatches);

            // ---- SUMMARY ----
            return _importLogic.FinalizeMatchResults(importCandidates);
        }
    }
}

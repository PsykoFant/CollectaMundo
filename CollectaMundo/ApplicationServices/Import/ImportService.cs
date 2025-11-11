using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;

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
        public async Task<(List<TempCardItem>, ColumnMapping)> LoadCsvFileAsync(string filePath, ProgressSinks? progress = null)
        {
            progress?.ProgressBarVisible.Report(true);
            progress?.Percent.Report(0);

            // This now calls ParseCsvFileAsync with the progress reporter
            var parsedItems = await _importLogic.ParseCsvFileAsync(filePath, progress?.Percent);

            var csvHeaders = parsedItems.FirstOrDefault()?.Fields.Keys.ToList() ?? [];
            var dbFields = await CardIdentifiersColumns();

            var mapping = new ColumnMapping
            {
                CsvHeaders = csvHeaders,
                DatabaseFields = dbFields,
                SelectedCsvHeader = csvHeaders.FirstOrDefault(),
                SelectedDatabaseField = dbFields.FirstOrDefault()
            };

            progress?.Percent.Report(100);
            progress?.ProgressBarVisible.Report(false);

            return (parsedItems, mapping);
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
        public async Task<ImportMatchSummaryDto> TryResolveUuidsFromMappedIdAsync(List<TempCardItem> importCandidates, ColumnMapping mapping)
        {
            var lookupValues = importCandidates.Select(item => item.Fields.TryGetValue(mapping.SelectedCsvHeader!, out var val) ? val : null)
                .Where(val => !string.IsNullOrWhiteSpace(val))
                .Select(val => val!) // safely assert non-null
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var idToUuids = await GetCardUuidsByIdFieldAsync(mapping.SelectedDatabaseField!, lookupValues);

            var summary = _importLogic.AssignUuidsToImportItems(importCandidates, idToUuids, mapping.SelectedCsvHeader!);

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
    }
}

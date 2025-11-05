using CollectaMundo.ApplicationServices.Shared;
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
        public async Task<(List<TempCardItem> parsedItems, ColumnMapping mapping)> LoadCsvFileAsync(string filePath)
        {
            var parsedItems = await _importLogic.ParseCsvFileAsync(filePath);
            var csvHeaders = parsedItems.FirstOrDefault()?.Fields.Keys.ToList() ?? [];

            var dbFields = await CardIdentifiersColumns();

            var mapping = new ColumnMapping
            {
                CsvHeaders = csvHeaders,
                DatabaseFields = dbFields,
                SelectedCsvHeader = csvHeaders.FirstOrDefault(),
                SelectedDatabaseField = dbFields.FirstOrDefault()
            };

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
            var lookupValues = importCandidates.Select(item => item.Fields.TryGetValue(mapping.SelectedCsvHeader!, out var val))
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .ToList();

            var idToUuids = await GetCardUuidsByIdFieldAsync(mapping.SelectedDatabaseField!, lookupValues);

            // Placeholder: replace with actual matching logic later
            return Task.FromResult(new ImportMatchSummaryDto
            {
                TotalItems = importCandidates.Count,
                ItemsWithUuid = 3,  // stub
                ItemsWithMultipleUuids = 1 // stub
            });
        }

        private async Task<List<string>> GetCardUuidsByIdFieldAsync(string identifierFieldName, List<string> values)
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

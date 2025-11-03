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

        public async Task<ColumnMapping> LoadCsvFileAsync(string filePath)
        {
            var parsedItems = await _importLogic.ParseCsvFileAsync(filePath);
            var csvHeaders = parsedItems.FirstOrDefault()?.Fields.Keys.ToList() ?? [];

            // TODO: Replace with database lookup later
            var dbFields = await GetCardIdentifiersColumns();

            var mapping = new ColumnMapping
            {
                CsvHeaders = csvHeaders,
                DatabaseFields = dbFields,
                SelectedCsvHeader = csvHeaders.FirstOrDefault(),
                SelectedDatabaseField = dbFields.FirstOrDefault()
            };

            return mapping;
        }

        private async Task<List<string>> GetCardIdentifiersColumns()
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
    }

}

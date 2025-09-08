using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.ImportExport;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public class ImportExportService(IImportExportRepo importExportRepo, IAppSettings settings) : IImportExportService
    {
        private readonly IImportExportRepo _importExportRepo = importExportRepo;
        private readonly IAppSettings _settings = settings;
        public async Task<OperationResult> ExportCollectionAsync()
        {
            try
            {
                await using var uow = new UnitOfWork();
                await uow.BeginAsync();

                var filePath = await _importExportRepo.ExportCollectionAsync(uow.CurrentConnection, _settings.BackupFolderPath);

                if (filePath == null)
                {
                    return new OperationResult(OperationResultCode.Empty, string.Empty);
                }
                else
                {
                    return new OperationResult(OperationResultCode.Success, filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating CSV backup: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Error creating CSV backup: {ex.Message}");
            }
        }

    }
}

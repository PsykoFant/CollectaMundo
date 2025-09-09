using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.ImportExport;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public class ImportExportService(IImportExportRepo importExportRepo, IAppSettings settings) : IImportExportService
    {
        private readonly IImportExportRepo _importExportRepo = importExportRepo;
        private readonly IAppSettings _settings = settings;
        public async Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                await using var uow = new UnitOfWork();
                await uow.BeginAsync();

                var filePath = await _importExportRepo.ExportCollectionAsync(uow.CurrentConnection, _settings.BackupFolderPath, ct);

                ct.ThrowIfCancellationRequested();

                if (filePath == null)
                    return new OperationResult(OperationResultCode.Empty, string.Empty);

                return new OperationResult(OperationResultCode.Success, filePath);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled backup");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating CSV backup: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Error creating CSV backup: {ex.Message}");
            }
        }
    }
}

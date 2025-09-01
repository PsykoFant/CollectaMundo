using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.ImportExport;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public class ImportExportService(IImportExportRepo importExportRepo) : IImportExportService
    {
        private readonly IImportExportRepo _importExportRepo = importExportRepo;
        public async Task<OperationResult> ExportCollectionAsync()
        {
            try
            {
                var uow = new UnitOfWork();
                await uow.BeginAsync();

                var filePath = await _importExportRepo.ExportCollectionAsync(uow.CurrentConnection);

                if (filePath == null)
                {
                    return new OperationResult(OperationResultCode.Empty, "Your collection is empty — nothing to back up.");
                }
                else
                {
                    return new OperationResult(OperationResultCode.Success, $"Backup created successfully at {filePath}");
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

using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public interface IImportExportService
    {
        Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default);
    }

}

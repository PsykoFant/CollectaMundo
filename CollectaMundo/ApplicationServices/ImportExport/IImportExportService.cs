using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public interface IImportExportService
    {
        Task<ExportResult> ExportCollectionAsync();
    }

}

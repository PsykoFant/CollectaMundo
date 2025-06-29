namespace CollectaMundo.ApplicationServices.ImportExport
{
    public interface IImportExportService
    {
        event Action<string> StatusMessage;
        Task ExportCollectionAsync();
    }

}

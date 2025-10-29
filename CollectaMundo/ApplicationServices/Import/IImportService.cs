namespace CollectaMundo.ApplicationServices.Import
{
    public interface IImportService
    {
        Task<string?> PromptForCsvFile();
    }
}

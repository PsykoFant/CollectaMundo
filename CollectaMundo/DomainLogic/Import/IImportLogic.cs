using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.DomainLogic.Import
{
    public interface IImportLogic
    {
        Task<List<TempCardItem>> ParseCsvFileAsync(string filePath);
    }
}

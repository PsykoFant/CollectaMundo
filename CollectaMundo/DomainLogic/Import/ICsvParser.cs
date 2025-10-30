using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.DomainLogic.Import
{
    public interface ICsvParser
    {
        Task<List<TempCardItem>> ParseCsvFileAsync(string filePath);
    }
}

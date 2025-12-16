namespace CollectaMundo.DomainLogic.Import.Models
{
    public class TempCardItem
    {
        // Pure CSV data
        public Dictionary<string, string> CsvFields { get; } = [];

        // Internal workflow identity
        public string TempItemImportKey { get; init; } = Guid.NewGuid().ToString();
    }

}

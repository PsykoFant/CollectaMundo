using CollectaMundo.DomainLogic.Import;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.ApplicationServices.Import
{
    public class ImportService(IImportRepo importRepo, IFileSystemPicker fileSystemPicker, ICsvParser csvParser) : IImportService
    {
        private readonly IImportRepo _importRepo = importRepo;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;
        private readonly ICsvParser _csvParser = csvParser;

        public string? PromptForCsvFile()
        {
            var file = _fileSystemPicker.PickFile("Select your CSV file to import");
            return file;
        }
    }

}

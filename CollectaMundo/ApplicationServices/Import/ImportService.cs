using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.ApplicationServices.Import
{
    public class ImportService(IImportRepo importRepo, IFileSystemPicker fileSystemPicker) : IImportService
    {
        private readonly IImportRepo _importRepo = importRepo;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;

        public string? PromptForCsvFile()
        {
            var file = _fileSystemPicker.PickFile("Select your CSV file to import");
            return file;
        }
    }

}

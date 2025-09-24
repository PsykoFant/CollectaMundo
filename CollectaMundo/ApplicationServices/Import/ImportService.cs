using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Data.Import;

namespace CollectaMundo.ApplicationServices.Import
{
    public class ImportService(IImportRepo importExportRepo, IAppSettings settings) : IImportService
    {
        private readonly IImportRepo _importExportRepo = importExportRepo;
        private readonly IAppSettings _settings = settings;
    }
}

using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.Startup
{
    public static class StartupComposition
    {
        public static IStartupService Build(StatusViewModel statusVM, Action closeStatusWindow)
        {
            var settings = new JsonAppSettings();
            var dbFactory = new DbConnectionFactory(settings);
            var scryfallLookups = new ScryfallLookups();
            var schemaInitializer = new DatabaseSchemaInitializer();
            var missingPngRepo = new GenerateMissingPngRepository();
            var missingPngLogic = new GenerateMissingPngLogic();
            var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic);

            var prepService = new CardDatabasePreparationService(settings, dbFactory, schemaInitializer, missingPngService, statusVM);
            var integrityService = new DatabaseIntegrityService();

            return new StartupService(integrityService, prepService, closeStatusWindow, statusVM);
        }
    }
}

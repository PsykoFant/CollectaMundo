using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.Data.CardPrices;
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
            var scryfallLookups = new ScryfallLookups();

            // Generate missing PNG stack
            var missingPngRepo = new GenerateMissingPngRepository();
            var missingPngLogic = new GenerateMissingPngLogic();
            var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic);

            // Card prices stack
            var cardPriceRepo = new CardPriceRepository();
            var priceService = new CardPriceService(settings, cardPriceRepo);

            // Database services stack
            var schemaInitializer = new DatabaseSchemaRepository();
            var prepService = new CardDatabasePreparationService(settings, schemaInitializer, priceService, missingPngService, statusVM);
            var integrityService = new DatabaseIntegrityService(settings);

            return new StartupService(integrityService, prepService, closeStatusWindow, statusVM);
        }
    }
}

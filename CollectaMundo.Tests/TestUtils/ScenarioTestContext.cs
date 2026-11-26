using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.Infrastructure.Shared.Database;
using CollectaMundo.Infrastructure.Shared.Files;
using CollectaMundo.ViewModels;
using System.IO;

namespace CollectaMundo.Tests.TestUtils
{
    public sealed class ScenarioTestContext : IAsyncDisposable
    {
        public MainWindowViewModel MainVM { get; private set; } = null!;
        public IDbConnectionFactory DbFactory { get; private set; } = null!;
        public FilteringService FilteringService { get; private set; } = null!;
        public MemoryStream? LastCsvOutput { get; private set; }

        public static async Task<ScenarioTestContext> CreateAsync(InMemoryDatabaseFixture fx, IFacetUpdateScheduler? facetScheduler = null)
        {
            var context = new ScenarioTestContext
            {
                DbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(fx.DbName)
            };

            var csvFileWriter = new CsvFileWriter(async (_, writeContent, _) =>
            {
                using var output = new MemoryStream();
                await writeContent(output);
                context.LastCsvOutput = output;
            });

            var (mainVM, _, filteringService) = await TestAppBuilder.BuildAsync(
                fx,
                context.DbFactory,
                facetScheduler: facetScheduler,
                csvFileWriterOverride: csvFileWriter);

            context.MainVM = mainVM;
            context.FilteringService = filteringService;

            return context;
        }

        public ValueTask DisposeAsync()
        {
            MainVM.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.Infrastructure.Shared.Database;
using CollectaMundo.ViewModels;

namespace CollectaMundo.Tests.TestUtils
{
    public sealed class ScenarioTestContext : IAsyncDisposable
    {
        public MainWindowViewModel MainVM { get; private set; } = null!;
        public IDbConnectionFactory DbFactory { get; private set; } = null!;
        public FilteringService FilteringService { get; private set; } = null!;

        public static async Task<ScenarioTestContext> CreateAsync(InMemoryDatabaseFixture fx)
        {
            var context = new ScenarioTestContext
            {
                DbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(fx.DbName)
            };

            var (mainVM, _, filteringService) = await TestAppBuilder.BuildAsync(fx, context.DbFactory);

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

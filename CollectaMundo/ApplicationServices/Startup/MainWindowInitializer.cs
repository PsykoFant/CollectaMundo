using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Startup
{
    public class MainWindowInitializer
    {
        private readonly IAppSettings _settings;
        private readonly IDbConnectionFactory _dbFactory;

        public MainWindowInitializer()
        {
            _settings = new JsonAppSettings();
            _dbFactory = new DbConnectionFactory(_settings);
        }
        public async Task InitializeAsync(List<(CardViewModel, CardListQuerySpec)> cardSpecs, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            try
            {
                await uow.BeginAsync();
                var conn = uow.CurrentConnection;

                var cardlistRepo = new CardListRepository();
                var filterRepo = new FilterInitDefaultsRepository();


                var cardTasks = new Task<IReadOnlyList<CardSet>>[cardSpecs.Count];

                for (int i = 0; i < cardSpecs.Count; i++)
                {
                    var spec = cardSpecs[i];
                    cardTasks[i] = cardlistRepo.QueryAsync(spec.Item2.Sql, conn, spec.Item2.Mapper);
                }

                var cardsResults = await Task.WhenAll(cardTasks);

                Debug.WriteLine("[InitializeAsync] All card queries completed");

                for (int i = 0; i < cardSpecs.Count; i++)
                {
                    Debug.WriteLine($"[InitializeAsync] Setting {cardSpecs[i].Item2} cards to ViewModel");
                    cardSpecs[i].Item1.Cards = [.. cardsResults[i]];
                }

                var filterDefaults = await filterRepo.GetFilterDefaultsAsync(conn);

                filters.Clear();
                foreach (var def in filterDefaults)
                {
                    filters[def.CriteriaKey] = new FilterItemViewModel(
                        def.CriteriaKey,
                        def.FilterOptions,
                        def.DefaultText,
                        def.ReadableLabel,
                        filterVM,
                        def.NumericCriteria);
                }

                Debug.WriteLine("[InitializeAsync] Filter defaults populated");
                await uow.CommitAsync();

            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                Debug.WriteLine($"[InitializeAsync] Exception caught: {ex.Message}");
            }
            finally
            {
                Debug.WriteLine($"[InitializeAsync] Connection state before dispose: {uow.CurrentConnection.State}");

                await uow.DisposeAsync();
                Debug.WriteLine("[InitializeAsync] UnitOfWork disposed");

                // Force GC collection to test cleanup behavior
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Debug.WriteLine("[InitializeAsync] Initialization complete");
        }

    }

}

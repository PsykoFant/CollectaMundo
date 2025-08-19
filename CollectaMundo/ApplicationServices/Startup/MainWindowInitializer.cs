using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Startup
{
    public class MainWindowInitializer
    {
        public static async Task InitializeAllCardLists(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork();
            try
            {
                var cardlistRepo = new CardListRepository();
                var filterRepo = new FilterInitDefaultsRepository();

                await uow.BeginAsync();
                var conn = uow.CurrentConnection;

                // 1) Single heavy read (AllCards cores)
                Debug.WriteLine("[InitializeAllCardLists] Loading cores from view_allCards…");
                var cores = await cardlistRepo.QueryAllCardsCoresAsync(conn);

                // 2) Build index by UUID for fast join
                var byUuid = new Dictionary<string, CardCore>(cores.Count, System.StringComparer.OrdinalIgnoreCase);
                foreach (var core in cores)
                    byUuid[core.Uuid] = core;

                // 3) Project cores -> CardSet (AllCards VM)
                Debug.WriteLine("[InitializeAllCardLists] Projecting AllCards…");
                var allCards = cores.AsParallel().AsOrdered().Select(CardSet.FromCore).ToList();
                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCards;

                // 4) Load myCollection rows (table only)
                Debug.WriteLine("[InitializeAllCardLists] Loading myCollection table…");
                var rows = await cardlistRepo.ReadMyCollection(conn);

                // 5) Join rows -> CardSet via shared core
                Debug.WriteLine("[InitializeAllCardLists] Projecting MyCollection from cores…");
                var myCollection = rows
                    .AsParallel()
                    .Select(r =>
                    {
                        if (!byUuid.TryGetValue(r.Uuid, out var core))
                        {
                            // If a UUID is missing from AllCards (edge case), skip or log:
                            Debug.WriteLine($"[Init M2] UUID not found in AllCards: {r.Uuid}");
                            return null;
                        }
                        return CardSet.FromCoreWithCollection(
                            core,
                            r.Id,
                            r.CardsOwned,
                            r.CardsForTrade,
                            r.Condition,
                            r.Language,
                            r.Finish);
                    })
                    .Where(c => c != null)!
                    .ToList();

                myCollectionVM.Cards = myCollection;
                myCollectionVM.FilteredCards = myCollection;

                // 6) Initialize filters and defaults (no cardSpecs/cardsResults here)
                Debug.WriteLine("[Init M1] Loading filter defaults…");
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
                Debug.WriteLine("[Init M1] Filter defaults populated");

                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        //public static async Task InitializeAsync(List<(CardViewModel, CardListQuerySpec)> cardSpecs, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        //{
        //    await using var uow = new UnitOfWork();
        //    try
        //    {
        //        await uow.BeginAsync();
        //        var conn = uow.CurrentConnection;

        //        var cardlistRepo = new CardListRepository();
        //        var filterRepo = new FilterInitDefaultsRepository();

        //        // Initializt and load card lists in parallel

        //        var cardTasks = new Task<IReadOnlyList<CardSet>>[cardSpecs.Count];

        //        for (int i = 0; i < cardSpecs.Count; i++)
        //        {
        //            var spec = cardSpecs[i];
        //            cardTasks[i] = cardlistRepo.QueryAsync(spec.Item2.Sql, conn, spec.Item2.Mapper);
        //        }

        //        var cardsResults = await Task.WhenAll(cardTasks);

        //        for (int i = 0; i < cardSpecs.Count; i++)
        //        {
        //            Debug.WriteLine($"[InitializeAsync] Setting {cardSpecs[i].Item2} cards to ViewModel");
        //            cardSpecs[i].Item1.Cards = [.. cardsResults[i]];
        //        }

        //        // Initialize filters and filter defaults
        //        var filterDefaults = await filterRepo.GetFilterDefaultsAsync(conn);

        //        filters.Clear();
        //        foreach (var def in filterDefaults)
        //        {
        //            filters[def.CriteriaKey] = new FilterItemViewModel(
        //                def.CriteriaKey,
        //                def.FilterOptions,
        //                def.DefaultText,
        //                def.ReadableLabel,
        //                filterVM,
        //                def.NumericCriteria);
        //        }

        //        Debug.WriteLine("[InitializeAsync] Filter defaults populated");
        //        await uow.CommitAsync();

        //    }
        //    catch (Exception ex)
        //    {
        //        await uow.RollbackAsync();
        //        Debug.WriteLine($"[InitializeAsync] Exception caught: {ex.Message}");
        //    }
        //    finally
        //    {
        //        await uow.DisposeAsync();

        //        // Force GC collection 
        //        GC.Collect();
        //        GC.WaitForPendingFinalizers();
        //        GC.Collect();
        //    }
        //}
    }
}

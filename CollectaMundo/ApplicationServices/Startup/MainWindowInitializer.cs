using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardLists.Images;
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
                await uow.BeginAsync();
                var conn = uow.CurrentConnection;

                var cardlistRepo = new CardListRepository();
                var filterRepo = new FilterInitDefaultsRepository();

                // 0) Mana-cost image cache: load once, share across all cards
                Debug.WriteLine("[InitializeAllCardLists] Loading mana-cost image cache…");
                var bytesMap = await cardlistRepo.ReadManaCostImagesAsync(conn);
                var manaCache = new ManaCostImageCache(bytesMap);
                CardSet.ManaCostImageProvider = manaCache;

                // 1) Single heavy read (AllCards cores)
                Debug.WriteLine("[InitializeAllCardLists] Loading cores from view_allCards…");
                var cores = await cardlistRepo.ReadAllCardsCoresAsync(conn);

                // 2) Build index by UUID for fast join
                var byUuid = new Dictionary<string, CardCore>(cores.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var core in cores)
                {
                    byUuid[core.Uuid] = core;
                }

                // 3) Project cores -> CardSet (AllCards VM)
                Debug.WriteLine("[InitializeAllCardLists] Projecting AllCards…");
                var allCards = cores
                    .AsParallel()
                    .AsOrdered()
                    .Select(CardSet.FromCore)
                    .ToList();

                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCards;

                // 4) Load myCollection rows (table only)
                Debug.WriteLine("[InitializeAllCardLists] Loading myCollection table…");
                var rows = await cardlistRepo.ReadMyCollectionAsync(conn); // returns rows with Id, Uuid, CardsOwned, CardsForTrade, Condition, Language, Finish

                // 5) Join rows -> CardSet via shared core
                Debug.WriteLine("[InitializeAllCardLists] Projecting MyCollection from cores…");
                var myCollection = rows.AsParallel().Select(r =>
                    {
                        if (!byUuid.TryGetValue(r.Uuid, out var core))
                        {
                            Debug.WriteLine($"[InitializeAllCardLists] UUID not found in AllCards: {r.Uuid}");
                            return null; // nullable here
                        }

                        return CardSet.FromCoreWithCollection(
                            core,
                            r.Id,
                            r.CardsOwned,
                            r.CardsForTrade,
                            r.Condition,
                            r.Language,
                            r.Finish);
                    }).Where(c => c != null).Cast<CardSet>().ToList();

                myCollectionVM.Cards = myCollection;
                myCollectionVM.FilteredCards = myCollection;

                // 6) Initialize filters and defaults
                Debug.WriteLine("[InitializeAllCardLists] Loading filter defaults…");
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
                Debug.WriteLine("[InitializeAllCardLists] Filter defaults populated");

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

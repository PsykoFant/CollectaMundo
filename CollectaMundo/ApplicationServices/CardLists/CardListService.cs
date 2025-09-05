using CollectaMundo.ApplicationServices.CardLists.Lookups;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.ViewModels;
using System.Diagnostics;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(ICardListRepository cardListRepo, IFilterDefaultsLogic filterLogic, ICardLookupsService lookupService, ICardCoreAggregator aggregator) : ICardListService
    {
        private readonly ICardListRepository _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterLogic = filterLogic;
        private readonly ICardLookupsService _lookupService = lookupService;
        private readonly ICardCoreAggregator _aggregator = aggregator;
        public async Task InitializeCardListsAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork();
            try
            {
                await uow.BeginReadOnlyAsync();
                var conn = uow.CurrentConnection;

                var dbIoSw = Stopwatch.StartNew();

                // Phase 1: DB I/O (sequential)
                var lookupPackageTask = _lookupService.LoadLookupDataAsync(conn, CardLookupsOptions.All);
                var coreDtosTask = _cardListRepo.ReadAllCardsCoreDtosAsync(conn);
                var collectionRowsTask = _cardListRepo.ReadMyCollectionAsync(conn);

                await Task.WhenAll(lookupPackageTask, coreDtosTask, collectionRowsTask);
                await uow.CommitAsync();

                dbIoSw.Stop();
                Debug.WriteLine($"[InitializeCardListsAsync] phase 1 (DB I/O): {dbIoSw.ElapsedMilliseconds} ms");

                // Phase 2a: Static provider setup (must be before FromCore)
                var lookupPackage = lookupPackageTask.Result;
                CardSet.ManaCostImages = lookupPackage.ManaCostImages;
                CardSet.SetIconImages = lookupPackage.SetIconImages;
                CardSet.SetMetaProvider = lookupPackage.SetMetaProvider;
                CardSet.PriceMetaProvider = lookupPackage.PriceMetaProvider;

                // Phase 2b: Hydrate and aggregate
                var coreDtos = coreDtosTask.Result;
                var collectionRows = collectionRowsTask.Result;

                var phase2bSw = Stopwatch.StartNew();

                var cores = new CardCore[coreDtos.Count];
                Parallel.For(0, coreDtos.Count, i => { cores[i] = CardCore.FromDto(coreDtos[i]); });

                var aggregatedCores = _aggregator.Aggregate(cores);
                var byUuid = aggregatedCores.ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

                phase2bSw.Stop();
                Debug.WriteLine($"[InitializeCardListsAsync] phase 2b (Hydrate and aggregate): {phase2bSw.ElapsedMilliseconds} ms");

                // PHASE 3a, 3b in parallel
                var phase3abSw = Stopwatch.StartNew();
                var allCardsTask = Task.Run(() =>
                {
                    var allCards = aggregatedCores
                        .AsParallel()
                        .AsOrdered()
                        .Select(CardSet.FromCore)
                        .ToList();

                    var sortSw = Stopwatch.StartNew();
                    allCardsVM.Cards = SortCards(allCards);
                    allCardsVM.FilteredCards = allCardsVM.Cards;
                    sortSw.Stop();
                    Debug.WriteLine($"Sorted allCards in {sortSw.ElapsedMilliseconds} ms");
                    return allCards;
                });

                var myCollectionTask = Task.Run(() =>
                {
                    var myCollection = collectionRows
                        .AsParallel()
                        .Select(r =>
                            byUuid.TryGetValue(r.Uuid, out var core)
                                ? CardSet.FromCoreWithCollection(core, r.Id, r.CardsOwned, r.CardsForTrade, r.Condition, r.Language, r.Finish)
                                : null)
                        .Where(c => c is not null)
                        .Cast<CardSet>()
                        .ToList();

                    myCollectionVM.Cards = SortCards(myCollection);
                    myCollectionVM.FilteredCards = myCollectionVM.Cards;
                    return myCollection;
                });
                await Task.WhenAll(allCardsTask, myCollectionTask); // Required

                phase3abSw.Stop();
                Debug.WriteLine($"[InitializeCardListsAsync] phase 3a and 3b (build AllCards and MyCollection objects): {phase3abSw.ElapsedMilliseconds} ms");

                var phase3cSw = Stopwatch.StartNew();

                var defs = _filterLogic.Build(allCardsTask.Result, myCollectionTask.Result);
                filters.Clear();
                foreach (var def in defs)
                {
                    filters[def.CriteriaKey] = new FilterItemViewModel(
                        def.CriteriaKey,
                        def.FilterOptions,
                        def.DefaultText,
                        def.ReadableLabel,
                        filterVM,
                        def.NumericCriteria);
                }

                phase3cSw.Stop();
                Debug.WriteLine($"[InitializeCardListsAsync] phase 3c (build filters): {phase3cSw.ElapsedMilliseconds} ms");

            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        public async Task ReloadPriceLookupsAsync(string retailerKey)
        {
            await using var uow = new UnitOfWork();
            await uow.BeginReadOnlyAsync();
            await _lookupService.ReloadPricesAsync(uow.CurrentConnection, retailerKey);
            await uow.CommitAsync();
        }
        private static List<CardSet> SortCards(IEnumerable<CardSet> cards)
        {
            static int ColorRank(string? colorCode)
            {
                return colorCode switch
                {
                    "W" => 0,
                    "U" => 1,
                    "B" => 2,
                    "R" => 3,
                    "G" => 4,
                    "C" => 5,
                    _ => 6 // Any unrecognized or multicolor at the end
                };
            }

            return [.. cards
                .OrderByDescending(c => c.ReleaseDate) // Assuming DateTime or sortable type
                .ThenBy(c =>
                {
                    var colors = c.Colors?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (colors is null || colors.Length == 0) { return 6; } return colors.Min(ColorRank); // Use lowest rank as primary color
                })
                .ThenBy(c => c.Types, StringComparer.OrdinalIgnoreCase)];
        }

    }
}


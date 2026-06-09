using CollectaMundo.ApplicationServices.CollectionMaterialization;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Filtering;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(IUnitOfWorkRunner uowRunner, ICardListRepo cardListRepo, IFilterDefaultsLogic filterDefaultsLogic, IKeyedDataProviderService keyedDataProviderService, ICardCoreAggregator aggregator, ICollectionMaterializer collectionMaterializer) : ICardListService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardListRepo _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterDefaultsLogic = filterDefaultsLogic;
        private readonly IKeyedDataProviderService _keyedDataProviderService = keyedDataProviderService;
        private readonly ICardCoreAggregator _aggregator = aggregator;
        private readonly ICollectionMaterializer _collectionMaterializer = collectionMaterializer;
        public async Task InitializeCardListsAsync(CardListViewModel allCardsVM, CardListViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            var dbIoSw = Stopwatch.StartNew();

            // Phase 1: DB I/O
            var (lookupPackage, coreDtos, collectionRows) = await _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                var lookupPackageTask = _keyedDataProviderService.LoadKeyedDataAsync(conn, KeyedDataProviderOptions.All);
                var coreDtosTask = _cardListRepo.ReadAllCardsCoreDtosAsync(conn);
                var collectionRowsTask = _cardListRepo.ReadMyCollectionAsync(conn);

                await Task.WhenAll(lookupPackageTask, coreDtosTask, collectionRowsTask);

                return (lookupPackageTask.Result, coreDtosTask.Result, collectionRowsTask.Result);
            });

            dbIoSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 1 (DB I/O): {dbIoSw.ElapsedMilliseconds} ms");

            // Phase 2a: Static provider setup (must be before FromCore)
            CardSet.ManaCostImages = lookupPackage.ManaCostImages;
            CardSet.SetIconImages = lookupPackage.SetIconImages;
            CardSet.SetMetaProvider = lookupPackage.SetMetaProvider;
            CardSet.PriceMetaProvider = lookupPackage.PriceMetaProvider;

            // Phase 2b: Hydrate and aggregate
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

                allCardsVM.Cards = SortCards(allCards);

                allCardsVM.FilteredCards = allCardsVM.Cards;
                return allCards;
            });

            var myCollectionTask = Task.Run(() =>
            {
                var myCollection = _collectionMaterializer.MaterializeRows(collectionRows, byUuid).ToList();

                myCollectionVM.Cards = SortCards(myCollection);
                myCollectionVM.FilteredCards = myCollectionVM.Cards;
                return myCollection;
            });

            await Task.WhenAll(allCardsTask, myCollectionTask);

            phase3abSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 3a and 3b (build AllCards and MyCollection objects): {phase3abSw.ElapsedMilliseconds} ms");

            var phase3cSw = Stopwatch.StartNew();

            var filterDefaults = _filterDefaultsLogic.Build(allCardsTask.Result, myCollectionTask.Result);
            filters.Clear();

            foreach (var def in filterDefaults)
            {
                filters[def.CriteriaKey] = new FilterItemViewModel(
                    def.CriteriaKey,
                    def.FilterOptions,
                    def.DefaultText,
                    def.ReadableLabel,
                    filterVM,
                    new FilterItemSearchLogic(),
                    def.NumericCriteria);
            }

            phase3cSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 3c (build filters): {phase3cSw.ElapsedMilliseconds} ms");
        }
        public async Task ReloadPriceLookupsAsync(string retailerKey)
        {
            await _keyedDataProviderService.ResetPricesMetaProviderAsync(retailerKey);
        }

        // helper to sort cards in the desired order
        private static List<CardSet> SortCards(IEnumerable<CardSet> cards)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int ColorRankFast(string? colors)
            {
                // W(0), U(1), B(2), R(3), G(4), MULTI(5), C(6), Unknown(7)
                if (colors is null)
                {
                    return 7;
                }

                // Monocolor -> exactly one char
                if (colors.Length == 1)
                {
                    return colors[0] switch
                    {
                        'W' => 0,
                        'U' => 1,
                        'B' => 2,
                        'R' => 3,
                        'G' => 4,
                        _ => 7,
                    };
                }

                // Anything longer than 1 char we treat as multicolor (no allocations / parsing)
                return 5;
            }

            return [.. cards
                .OrderByDescending(c => c.ReleaseDate)
                .ThenBy(c => c.SetCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => ColorRankFast(c.Colors))
                .ThenBy(c => c.Types, StringComparer.OrdinalIgnoreCase)];
        }
    }
}


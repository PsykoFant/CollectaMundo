using CollectaMundo.ApplicationServices.CardLists.CardLookups;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Filtering;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(IDbConnectionFactory dbFactory, ICardListRepo cardListRepo, IFilterDefaultsLogic filterLogic, ICardLookupsService lookupService, ICardCoreAggregator aggregator) : ICardListService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardListRepo _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterLogic = filterLogic;
        private readonly ICardLookupsService _lookupService = lookupService;
        private readonly ICardCoreAggregator _aggregator = aggregator;
        public async Task InitializeCardListsAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            try
            {
                await uow.BeginReadOnlyAsync();
                var conn = uow.CurrentConnection;

                var dbIoSw = Stopwatch.StartNew();

                // Phase 1: DB I/O
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
                    var allCards = aggregatedCores.AsParallel().AsOrdered().Select(CardSet.FromCore).ToList();

                    var sortSw = Stopwatch.StartNew();
                    allCardsVM.Cards = SortCards(allCards);
                    sortSw.Stop();
                    Debug.WriteLine($"[InitializeCardListsAsync]   - sorting AllCards: {sortSw.ElapsedMilliseconds} ms");

                    allCardsVM.FilteredCards = allCardsVM.Cards;
                    return allCards;
                });

                var myCollectionTask = Task.Run(() =>
                {
                    var myCollection = collectionRows
                        .AsParallel()
                        .Select(r => byUuid.TryGetValue(r.Identity.Uuid, out var core)
                        ? CardSet.FromCoreWithCollection(
                            core,
                            r.CardId,
                            r.CardsOwned,
                            r.CardsForTrade,
                            r.Identity.Condition,
                            r.Identity.Language,
                            r.Identity.Finish)
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
                        new FilterItemSearchLogic(),
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
            await _lookupService.ResetPricesMetaProviderAsync(retailerKey);
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


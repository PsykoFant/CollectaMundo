using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using System.Diagnostics;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(IUnitOfWorkRunner uowRunner, ICardListRepo cardListRepo, IFilterDefaultsLogic filterDefaultsLogic, IKeyedDataProviderService keyedDataProviderService, ICardLegalityProviderService cardLegalityProviderService) : ICardListService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardListRepo _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterDefaultsLogic = filterDefaultsLogic;
        private readonly IKeyedDataProviderService _keyedDataProviderService = keyedDataProviderService;
        private readonly ICardLegalityProviderService _cardLegalityProviderService = cardLegalityProviderService;
        public async Task InitializeCardListsAsync(CardListViewModel<PrintingCard> allCardsVM, CardListViewModel<CollectionCard> myCollectionVM, CardListViewModel<OracleCard> oracleCardsVM, Dictionary<string, FilterItemViewModel> filters, FilterPanelViewModel filterVM)
        {
            var phase1Sw = Stopwatch.StartNew();

            // Phase 1: Load database-backed startup data

            // Small workload: keep sequential.
            var keyedSw = Stopwatch.StartNew();
            var lookupPackage = await _uowRunner.ExecuteReadOnlyAsync(conn => _keyedDataProviderService.LoadKeyedDataAsync(conn, KeyedDataProviderOptions.All));

            keyedSw.Stop();

            Debug.WriteLine($"[Phase 1] Keyed data: {keyedSw.ElapsedMilliseconds} ms");

            // Heavy workloads:
            // run concurrently on separate SQLite connections / worker threads.
            var legalityTask = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();

                await _uowRunner.ExecuteReadOnlyAsync(conn => _cardLegalityProviderService.LoadLegalitiesAsync(conn));

                sw.Stop();

                Debug.WriteLine($"[Phase 1] Legalities: {sw.ElapsedMilliseconds} ms");
            });

            var printingRowsTask = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();

                var rows = await _uowRunner.ExecuteReadOnlyAsync(conn => _cardListRepo.ReadAllCardPrintingDbRowsAsync(conn));

                sw.Stop();

                Debug.WriteLine($"[Phase 1] Printing rows: {sw.ElapsedMilliseconds} ms");

                return rows;
            });

            await Task.WhenAll(legalityTask, printingRowsTask);

            var printingRows = await printingRowsTask;

            // Small workload: keep sequential.
            var collectionSw = Stopwatch.StartNew();

            var collectionRows = await _uowRunner.ExecuteReadOnlyAsync(
                conn => _cardListRepo.ReadMyCollectionAsync(conn));

            collectionSw.Stop();

            Debug.WriteLine($"[Phase 1] Collection rows: {collectionSw.ElapsedMilliseconds} ms");

            phase1Sw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 1 (load startup data): {phase1Sw.ElapsedMilliseconds} ms");


            // Phase 2a: Static provider setup
            CardDataProviders.ManaCostImages = lookupPackage.ManaCostImages;
            CardDataProviders.SetIconImages = lookupPackage.SetIconImages;
            CardDataProviders.SetMetaProvider = lookupPackage.SetMetaProvider;
            CardDataProviders.PriceMetaProvider = lookupPackage.PriceMetaProvider;

            // Phase 2b: Hydrate and aggregate
            var phase2bSw = Stopwatch.StartNew();

            var gen0Start = GC.CollectionCount(0);
            var gen1Start = GC.CollectionCount(1);
            var gen2Start = GC.CollectionCount(2);


            // Hydrate PrintingCard objects
            var hydrateSw = Stopwatch.StartNew();

            var printings = new PrintingCard[printingRows.Count];

            Parallel.For(0, printingRows.Count, i =>
            {
                var row = printingRows[i];
                var uuid = row.Uuid ?? string.Empty;

                _cardLegalityProviderService.MasksByUuid.TryGetValue(
                    uuid,
                    out var legalityMasks);

                printings[i] = PrintingCardFactory.FromRow(
                    row,
                    legalityMasks);
            });

            hydrateSw.Stop();

            var gen0AfterHydrate = GC.CollectionCount(0);
            var gen1AfterHydrate = GC.CollectionCount(1);
            var gen2AfterHydrate = GC.CollectionCount(2);

            Debug.WriteLine(
                $"[Phase 2b] Hydrate {printings.Length} printings: " +
                $"{hydrateSw.ElapsedMilliseconds} ms");

            Debug.WriteLine(
                $"[Phase 2b] GC during hydrate: " +
                $"Gen0 +{gen0AfterHydrate - gen0Start}, " +
                $"Gen1 +{gen1AfterHydrate - gen1Start}, " +
                $"Gen2 +{gen2AfterHydrate - gen2Start}");


            // Aggregate printings
            var aggregateSw = Stopwatch.StartNew();

            var aggregatedPrintings =
                PrintingCardAggregator.Aggregate(printings);

            aggregateSw.Stop();

            var gen0AfterAggregate = GC.CollectionCount(0);
            var gen1AfterAggregate = GC.CollectionCount(1);
            var gen2AfterAggregate = GC.CollectionCount(2);

            Debug.WriteLine(
                $"[Phase 2b] Aggregate printings: " +
                $"{aggregateSw.ElapsedMilliseconds} ms");

            Debug.WriteLine(
                $"[Phase 2b] GC during aggregate: " +
                $"Gen0 +{gen0AfterAggregate - gen0AfterHydrate}, " +
                $"Gen1 +{gen1AfterAggregate - gen1AfterHydrate}, " +
                $"Gen2 +{gen2AfterAggregate - gen2AfterHydrate}");


            // Build UUID lookup
            var dictionarySw = Stopwatch.StartNew();

            var printingByUuid = aggregatedPrintings
                .Where(p => !string.IsNullOrWhiteSpace(p.Uuid))
                .ToDictionary(
                    p => p.Uuid,
                    StringComparer.OrdinalIgnoreCase);

            dictionarySw.Stop();

            var gen0AfterDictionary = GC.CollectionCount(0);
            var gen1AfterDictionary = GC.CollectionCount(1);
            var gen2AfterDictionary = GC.CollectionCount(2);

            Debug.WriteLine(
                $"[Phase 2b] Build UUID dictionary: " +
                $"{dictionarySw.ElapsedMilliseconds} ms");

            Debug.WriteLine(
                $"[Phase 2b] GC during dictionary: " +
                $"Gen0 +{gen0AfterDictionary - gen0AfterAggregate}, " +
                $"Gen1 +{gen1AfterDictionary - gen1AfterAggregate}, " +
                $"Gen2 +{gen2AfterDictionary - gen2AfterAggregate}");


            phase2bSw.Stop();

            Debug.WriteLine(
                $"[InitializeCardListsAsync] Phase 2b " +
                $"(hydrate and aggregate): {phase2bSw.ElapsedMilliseconds} ms");

            // Phase 3a + 3b:
            // build independent application-facing collections concurrently.
            var phase3abSw = Stopwatch.StartNew();

            var allCardsTask = Task.Run(() =>
            {
                var allCards = SortCards(aggregatedPrintings);

                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCardsVM.Cards;

                return allCards;
            });

            var myCollectionTask = Task.Run(() =>
            {
                var myCollection = collectionRows.Select(row =>
                    {
                        if (!printingByUuid.TryGetValue(row.Identity.Uuid, out var printing))
                        {
                            throw new InvalidOperationException($"Cannot materialize collection card. " + $"Printing not found for UUID '{row.Identity.Uuid}'.");
                        }

                        return CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                    }).ToList();

                myCollectionVM.Cards = SortCards(myCollection);
                myCollectionVM.FilteredCards = myCollectionVM.Cards;

                return myCollection;
            });

            var oracleCardsTask = Task.Run(() =>
            {
                var oracleCards = aggregatedPrintings.Select(p => p.Oracle).Where(o => !string.IsNullOrWhiteSpace(o.ScryfallOracleId))
                .GroupBy(o => o.ScryfallOracleId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                oracleCardsVM.Cards = SortOracleCards(oracleCards);
                oracleCardsVM.FilteredCards = oracleCardsVM.Cards;

                return oracleCards;
            });

            await Task.WhenAll(allCardsTask, myCollectionTask, oracleCardsTask);

            phase3abSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3a/3b (build card lists): {phase3abSw.ElapsedMilliseconds} ms");


            // Phase 3c: Build filter defaults
            var phase3cSw = Stopwatch.StartNew();

            var filterDefaults = _filterDefaultsLogic.Build(allCardsTask.Result, myCollectionTask.Result);

            filters.Clear();

            foreach (var def in filterDefaults)
            {
                filters[def.CriteriaKey] =
                    new FilterItemViewModel(
                        def.CriteriaKey,
                        def.FilterOptions,
                        def.DefaultText,
                        def.ReadableLabel,
                        filterVM,
                        new FilterItemSearchLogic(),
                        def.NumericCriteria);
            }

            phase3cSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3c (build filters): {phase3cSw.ElapsedMilliseconds} ms");
        }
        public async Task ReloadPriceLookupsAsync(string retailerKey)
        {
            await _keyedDataProviderService.ResetPricesMetaProviderAsync(retailerKey);
        }
        private static List<TCard> SortCards<TCard>(IEnumerable<TCard> cards) where TCard : ICardListSortable
        {
            return
            [
                .. cards
            .OrderByDescending(c => c.ReleaseDate)
            .ThenBy(c => c.SetCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => CardSort.GetColorRank(c.Colors))
            .ThenBy(c => CardSort.GetTypeRank(c.Types, c.GamePlayCard))
            ];
        }
        private static List<OracleCard> SortOracleCards(IEnumerable<OracleCard> cards)
        {
            return
            [
                .. cards
            .OrderByDescending(c => c.GamePlayCard)
            .ThenBy(c => CardSort.GetTypeRank(c.Types, c.GamePlayCard))
            .ThenBy(c => CardSort.GetColorRank(c.Colors))
            .ThenBy(c => c.ManaValue)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Types, StringComparer.OrdinalIgnoreCase)
            ];
        }
    }
}


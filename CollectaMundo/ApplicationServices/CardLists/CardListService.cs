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

            #region Phase 1: Load database-backed startup data

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

            #endregion

            #region Phase 2: Hydrate and aggregate printing cards

            // Phase 2a: Static provider setup
            CardDataProviders.ManaCostImages = lookupPackage.ManaCostImages;
            CardDataProviders.SetIconImages = lookupPackage.SetIconImages;
            CardDataProviders.SetMetaProvider = lookupPackage.SetMetaProvider;
            CardDataProviders.PriceMetaProvider = lookupPackage.PriceMetaProvider;

            // Phase 2b: Hydrate and aggregate
            var phase2bSw = Stopwatch.StartNew();

            var printings = new PrintingCard[printingRows.Count];

            Parallel.For(0, printingRows.Count, i =>
            {
                var row = printingRows[i];
                var uuid = row.Uuid ?? string.Empty;

                _cardLegalityProviderService.MasksByUuid.TryGetValue(uuid, out var legalityMasks);

                printings[i] = PrintingCardFactory.FromRow(row, legalityMasks);
            });

            var aggregatedPrintings = PrintingCardAggregator.AggregatePrintingCards(printings);

            var printingByUuid = aggregatedPrintings.Where(p => !string.IsNullOrWhiteSpace(p.Uuid)).ToDictionary(p => p.Uuid, StringComparer.OrdinalIgnoreCase);

            phase2bSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 2b " + $"(hydrate and aggregate): {phase2bSw.ElapsedMilliseconds} ms");

            #endregion

            #region Phase 3: Build application-facing card lists and filters

            // Phase 3a + 3b:
            // BuildFilters independent application-facing collections concurrently.
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
                    .Select(g => g.First()).ToList();

                oracleCardsVM.Cards = SortOracleCards(oracleCards);
                oracleCardsVM.FilteredCards = oracleCardsVM.Cards;

                return oracleCards;
            });

            await Task.WhenAll(allCardsTask, myCollectionTask, oracleCardsTask);

            phase3abSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3a/3b " + $"(build card lists): {phase3abSw.ElapsedMilliseconds} ms");


            // Phase 3c: Build filter defaults
            var phase3cSw = Stopwatch.StartNew();

            var buildDefaultsSw = Stopwatch.StartNew();

            var filterDefaults = _filterDefaultsLogic.BuildFilters(allCardsTask.Result, myCollectionTask.Result);

            buildDefaultsSw.Stop();

            Debug.WriteLine($"[Phase 3c] Build filter defaults: " + $"{buildDefaultsSw.ElapsedMilliseconds} ms");

            var buildViewModelsSw = Stopwatch.StartNew();

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

            buildViewModelsSw.Stop();

            Debug.WriteLine($"[Phase 3c] Build FilterItemViewModels: " + $"{buildViewModelsSw.ElapsedMilliseconds} ms");

            phase3cSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3c " + $"(build filters): {phase3cSw.ElapsedMilliseconds} ms");

            #endregion
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


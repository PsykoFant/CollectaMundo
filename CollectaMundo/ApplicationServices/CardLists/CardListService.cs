using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.CardLists.Factories;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardData.Models;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using System.Diagnostics;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(IUnitOfWorkRunner uowRunner, ICardListRepo cardListRepo, IFilterDefaultsLogic filterDefaultsLogic, IKeyedDataProviderService keyedDataProviderService, ICardLegalityService cardLegalityService) : ICardListService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardListRepo _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterDefaultsLogic = filterDefaultsLogic;
        private readonly IKeyedDataProviderService _keyedDataProviderService = keyedDataProviderService;
        private readonly ICardLegalityService _cardLegalityService = cardLegalityService;
        public async Task InitializeCardListsAsync(CardListViewModel<PrintingCard> allCardsVM, CardListViewModel<CollectionCard> myCollectionVM, CardListViewModel<OracleCard> oracleCardsVM, Dictionary<string, FilterItemViewModel> filters, FilterPanelViewModel filterVM)
        {
            var phase1Sw = Stopwatch.StartNew();

            #region Phase 1: Load database-backed startup data

            // Small workload: keep sequential.
            var keyedSw = Stopwatch.StartNew();
            var lookupPackage = await _uowRunner.ExecuteReadOnlyAsync(conn => _keyedDataProviderService.LoadKeyedDataAsync(conn, KeyedDataProviderOptions.All));

            keyedSw.Stop();

            Debug.WriteLine($"[Phase 1] Keyed data: {keyedSw.ElapsedMilliseconds} ms");

            var legalityFormatsSw = Stopwatch.StartNew();

            await _uowRunner.ExecuteReadOnlyAsync(conn => _cardLegalityService.LoadFormatsAsync(conn));

            legalityFormatsSw.Stop();

            Debug.WriteLine($"[Phase 1] Legality formats: {legalityFormatsSw.ElapsedMilliseconds} ms");


            // Heavy workloads:
            // run concurrently on separate SQLite connections / worker threads.            

            var cardRowsTask = Task.Run(async () =>
            {
                return await _uowRunner.ExecuteReadOnlyAsync(async conn =>
                {
                    var canonicalSw = Stopwatch.StartNew();

                    var canonicalRows = await _cardListRepo.ReadCanonicalOracleFacesAsync(conn);

                    canonicalSw.Stop();

                    Debug.WriteLine($"[Phase 1] Canonical Oracle rows: {canonicalRows.Count:N0} rows, {canonicalSw.ElapsedMilliseconds} ms");

                    var printingSw = Stopwatch.StartNew();

                    var printingRows = await _cardListRepo.ReadPrintingCardRowsAsync(conn);

                    printingSw.Stop();

                    Debug.WriteLine($"[Phase 1] Printing rows: {printingRows.Count:N0} rows, {printingSw.ElapsedMilliseconds} ms");

                    return (CanonicalRows: canonicalRows, PrintingRows: printingRows);
                });
            });

            var (canonicalRows, printingRows) = await cardRowsTask;


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

            var oracleHydrationSw = Stopwatch.StartNew();
            var oracleByFace = BuildOracleCardsByFace(canonicalRows);

            oracleHydrationSw.Stop();

            Debug.WriteLine($"[Phase 2b] Hydrate canonical Oracle cards: {oracleByFace.Count:N0} cards, {oracleHydrationSw.ElapsedMilliseconds} ms");

            var printingHydrationSw = Stopwatch.StartNew();
            var printings = HydratePrintingCards(printingRows, oracleByFace);

            printingHydrationSw.Stop();

            Debug.WriteLine($"[Phase 2b] Hydrate printings: {printings.Length:N0} cards, {printingHydrationSw.ElapsedMilliseconds} ms");

            // Build Oracle-level lookup:
            // ScryfallOracleId -> union of playable formats across all exact printings of that Oracle card.
            var legalityLookupSw = Stopwatch.StartNew();

            _cardLegalityService.BuildOraclePlayableFormatsLookup(printings);

            legalityLookupSw.Stop();

            Debug.WriteLine($"[Phase 2b] Build Oracle printing-legality lookup: {legalityLookupSw.ElapsedMilliseconds} ms");

            var aggregationSw = Stopwatch.StartNew();

            var aggregatedPrintings = PrintingCardAggregator.AggregatePrintingCards(printings);

            aggregationSw.Stop();

            Debug.WriteLine($"[Phase 2b] Aggregate printings: {aggregationSw.ElapsedMilliseconds} ms");

            var printingByUuid = aggregatedPrintings.Where(p => !string.IsNullOrWhiteSpace(p.Uuid)).ToDictionary(p => p.Uuid, StringComparer.OrdinalIgnoreCase);

            phase2bSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 2b (hydrate and aggregate): {phase2bSw.ElapsedMilliseconds} ms");

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
                            var sourcePrinting = printings.FirstOrDefault(p => string.Equals(p.Uuid, row.Identity.Uuid, StringComparison.OrdinalIgnoreCase));

                            throw new InvalidOperationException(
                                $"Cannot materialize collection card. " +
                                $"Printing not found for UUID '{row.Identity.Uuid}'. " +
                                $"Present in source printings: {sourcePrinting is not null}. " +
                                $"Source side: '{sourcePrinting?.Oracle.Side ?? "<none>"}'. " +
                                $"Source other faces: " +
                                $"'{string.Join(",", sourcePrinting?.OtherFaceIds ?? [])}'.");
                        }

                        return CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                    }).ToList();

                myCollectionVM.Cards = SortCards(myCollection);
                myCollectionVM.FilteredCards = myCollectionVM.Cards;

                return myCollection;
            });

            var oracleCardsTask = Task.Run(() =>
            {
                var oracleCards = BuildAndSortOracleCards(aggregatedPrintings);

                oracleCardsVM.Cards = oracleCards;
                oracleCardsVM.FilteredCards = oracleCardsVM.Cards;

                return oracleCards;
            });

            await Task.WhenAll(allCardsTask, myCollectionTask, oracleCardsTask);

            phase3abSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3a/3b (build card lists): {phase3abSw.ElapsedMilliseconds} ms");


            var phase3cSw = Stopwatch.StartNew();

            var filterDefaults = _filterDefaultsLogic.BuildFilters(allCardsTask.Result, myCollectionTask.Result, _cardLegalityService.Formats);

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

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 3c " + $"(build filters): {phase3cSw.ElapsedMilliseconds} ms");

            #endregion
        }
        private static Dictionary<OracleFaceKey, OracleCard> BuildOracleCardsByFace(IReadOnlyList<CanonicalOracleFaceDbRow> rows)
        {
            var result = new Dictionary<OracleFaceKey, OracleCard>(rows.Count);

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var key = new OracleFaceKey(row.ScryfallOracleId, row.Side);
                var oracle = OracleCardFactory.FromRow(row);

                result.Add(key, oracle);
            }

            return result;
        }
        private static PrintingCard[] HydratePrintingCards(IReadOnlyList<PrintingCardDbRow> rows, IReadOnlyDictionary<OracleFaceKey, OracleCard> oracleByFace)
        {
            var printings = new PrintingCard[rows.Count];

            Parallel.For(0, rows.Count, i =>
            {
                var row = rows[i];
                var key = new OracleFaceKey(row.ScryfallOracleId, row.Side);

                if (!oracleByFace.TryGetValue(key, out var oracle))
                {
                    throw new InvalidOperationException($"Cannot materialize printing '{row.Uuid}'. Canonical Oracle face not found for OracleId='{row.ScryfallOracleId}', Side='{row.Side}'.");
                }

                printings[i] = PrintingCardFactory.FromRow(row, oracle);
            });

            return printings;
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
        private static List<OracleCard> BuildAndSortOracleCards(IEnumerable<PrintingCard> printings)
        {
            return
            [
                .. printings.Where(p => !string.IsNullOrWhiteSpace(p.ScryfallOracleId))
                .GroupBy(p => p.ScryfallOracleId!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(p => p.GamePlayCard)
                .ThenBy(p => CardSort.GetTypeRank(p.Types, p.GamePlayCard))
                .ThenBy(p => CardSort.GetColorRank(p.Colors))
                .ThenBy(p => p.ManaValue)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Types, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Oracle)
            ];
        }
    }
}


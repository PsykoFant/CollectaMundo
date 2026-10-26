using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.CardLists.Factories;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
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
            // Load Oracle cards and printing cards sequentially on one read-only connection, off the UI thread.

            var cardRowsTask = Task.Run(async () =>
            {
                return await _uowRunner.ExecuteReadOnlyAsync(async conn =>
                {
                    var oracleSw = Stopwatch.StartNew();

                    var oracleRows = await _cardListRepo.ReadOracleCardsAsync(conn);

                    oracleSw.Stop();

                    Debug.WriteLine($"[Phase 1] Oracle rows: {oracleRows.Count:N0} rows, {oracleSw.ElapsedMilliseconds} ms");

                    var printingSw = Stopwatch.StartNew();

                    var printingRows = await _cardListRepo.ReadPrintingCardRowsAsync(conn);

                    printingSw.Stop();

                    Debug.WriteLine($"[Phase 1] Printing rows: {printingRows.Count:N0} rows, {printingSw.ElapsedMilliseconds} ms");

                    return (OracleRows: oracleRows, PrintingRows: printingRows);
                });
            });

            var (oracleRows, printingRows) = await cardRowsTask;

            // Small workload: keep sequential.
            var collectionSw = Stopwatch.StartNew();

            var collectionRows = await _uowRunner.ExecuteReadOnlyAsync(
                conn => _cardListRepo.ReadMyCollectionAsync(conn));

            collectionSw.Stop();

            Debug.WriteLine($"[Phase 1] Collection rows: {collectionSw.ElapsedMilliseconds} ms");

            phase1Sw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 1 (load startup data): {phase1Sw.ElapsedMilliseconds} ms");

            #endregion

            #region Phase 2: Hydrate Oracle cards and physical printings

            // Phase 2a: Static provider setup
            CardDataProviders.ManaCostImages = lookupPackage.ManaCostImages;
            CardDataProviders.SetIconImages = lookupPackage.SetIconImages;
            CardDataProviders.SetMetaProvider = lookupPackage.SetMetaProvider;
            CardDataProviders.PriceMetaProvider = lookupPackage.PriceMetaProvider;

            // Phase 2b: Hydrate Oracle cards and exact physical printings
            var phase2bSw = Stopwatch.StartNew();

            // ---------------------------------------------------------
            // 1. Hydrate complete Oracle cards
            // ---------------------------------------------------------

            var oracleHydrationSw = Stopwatch.StartNew();

            var oracleById = BuildOracleCards(oracleRows);

            oracleHydrationSw.Stop();

            Debug.WriteLine($"[Phase 2b] Hydrate Oracle cards: {oracleById.Count:N0} cards, {oracleHydrationSw.ElapsedMilliseconds} ms");

            // ---------------------------------------------------------
            // 2. Hydrate exact physical printings
            // ---------------------------------------------------------

            var printingHydrationSw = Stopwatch.StartNew();
            var printings = HydratePrintingCards(printingRows, oracleById);

            printingHydrationSw.Stop();

            Debug.WriteLine($"[Phase 2b] Hydrate printings: {printings.Length:N0} cards, {printingHydrationSw.ElapsedMilliseconds} ms");

            // ---------------------------------------------------------
            // 3. Build Oracle-level legality lookup
            // ---------------------------------------------------------

            var legalityLookupSw = Stopwatch.StartNew();

            _cardLegalityService.BuildOraclePlayableFormatsLookup(printings);

            legalityLookupSw.Stop();

            Debug.WriteLine($"[Phase 2b] Build Oracle printing-legality lookup: {legalityLookupSw.ElapsedMilliseconds} ms");

            // ---------------------------------------------------------
            // 4. Build exact printing lookup by UUID
            // ---------------------------------------------------------

            // Retain every physical printing, including secondary faces.
            var printingByUuid = printings.ToDictionary(printing => printing.Uuid, StringComparer.OrdinalIgnoreCase);

            // ---------------------------------------------------------
            // 5. Select standalone printings for application-facing lists
            // ---------------------------------------------------------

            // Only side "" and side "a" represent standalone cards.
            var displayPrintings = printingRows.Where(row => IsPrimarySide(row.Side)).Select(row => printingByUuid[row.Uuid]).ToArray();

            // Identify Oracle cards that have at least one primary printing. Secondary-only Oracle identities, such as Brisela, are excluded.
            var standaloneOracleIds = displayPrintings.Select(printing => printing.ScryfallOracleId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Debug.WriteLine($"[Phase 2b] Physical printings: {printings.Length:N0}, standalone printings: {displayPrintings.Length:N0}, standalone Oracle cards: {standaloneOracleIds.Count:N0}");

            phase2bSw.Stop();

            Debug.WriteLine($"[InitializeCardListsAsync] Phase 2b (hydrate cards): {phase2bSw.ElapsedMilliseconds} ms");

            #endregion

            #region Phase 3: Build application-facing card lists and filters

            // Phase 3a + 3b:
            // Build independent application-facing card lists concurrently.
            var phase3abSw = Stopwatch.StartNew();

            var allCardsTask = Task.Run(() => SortCards(displayPrintings));
            var myCollectionTask = Task.Run(() =>
            {
                var myCollection = collectionRows.Select(row =>
                {
                    if (!printingByUuid.TryGetValue(row.Identity.Uuid, out var printing))
                    {
                        throw new InvalidOperationException(
                            $"Cannot materialize collection card. " +
                            $"Printing not found for UUID '{row.Identity.Uuid}'.");
                    }

                    return CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                }).ToList();

                return SortCards(myCollection);
            });
            var oracleCardsTask = Task.Run(() => SortOracleCards(oracleById.Values.Where(oracle => standaloneOracleIds.Contains(oracle.ScryfallOracleId))));

            await Task.WhenAll(allCardsTask, myCollectionTask, oracleCardsTask);

            // Assign on the captured WPF UI synchronization context.
            allCardsVM.Cards = allCardsTask.Result;
            allCardsVM.FilteredCards = allCardsVM.Cards;

            myCollectionVM.Cards = myCollectionTask.Result;
            myCollectionVM.FilteredCards = myCollectionVM.Cards;

            oracleCardsVM.Cards = oracleCardsTask.Result;
            oracleCardsVM.FilteredCards = oracleCardsVM.Cards;

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
        private static Dictionary<string, OracleCard> BuildOracleCards(IReadOnlyList<OracleCardDbRow> rows)
        {
            var result = new Dictionary<string, OracleCard>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var oracle = OracleCardFactory.FromRow(row);

                result.Add(row.ScryfallOracleId, oracle);
            }

            return result;
        }
        private static List<OracleCard> SortOracleCards(IEnumerable<OracleCard> oracleCards)
        {
            return
            [
                .. oracleCards
                .OrderByDescending(c => c.GamePlayCard)
                .ThenBy(c => CardSort.GetTypeRank(c.Types, c.GamePlayCard))
                .ThenBy(c => CardSort.GetColorRank(c.Colors))
                .ThenBy(c => c.ManaValue)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Types, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.ScryfallOracleId, StringComparer.Ordinal)
                ];
        }
        private static PrintingCard[] HydratePrintingCards(IReadOnlyList<PrintingCardDbRow> rows, IReadOnlyDictionary<string, OracleCard> oracleById)
        {
            var printings = new PrintingCard[rows.Count];

            Parallel.For(0, rows.Count, i =>
            {
                var row = rows[i];

                if (!oracleById.TryGetValue(row.ScryfallOracleId, out var oracle))
                {
                    throw new InvalidOperationException($"Cannot materialize printing '{row.Uuid}'. Oracle card not found for OracleId='{row.ScryfallOracleId}'.");
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
        private static bool IsPrimarySide(string? side)
        {
            return string.IsNullOrWhiteSpace(side) || string.Equals(side, "a", StringComparison.OrdinalIgnoreCase);
        }
    }
}


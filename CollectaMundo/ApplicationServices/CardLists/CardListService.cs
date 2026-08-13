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
            var dbIoSw = Stopwatch.StartNew();

            // Phase 1: DB I/O

            var (lookupPackage, printingRows, collectionRows) = await _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                var lookupPackageTask = _keyedDataProviderService.LoadKeyedDataAsync(conn, KeyedDataProviderOptions.All); var legalityTask = _cardLegalityProviderService.LoadAsync(conn);
                var printingRowsTask = _cardListRepo.ReadAllCardPrintingDbRowsAsync(conn);
                var collectionRowsTask = _cardListRepo.ReadMyCollectionAsync(conn);

                await Task.WhenAll(lookupPackageTask, legalityTask, printingRowsTask, collectionRowsTask);

                return (lookupPackageTask.Result, printingRowsTask.Result, collectionRowsTask.Result);
            });

            dbIoSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 1 (DB I/O): {dbIoSw.ElapsedMilliseconds} ms");

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

            var aggregatedPrintings = PrintingCardAggregator.Aggregate(printings);
            var printingByUuid = aggregatedPrintings.Where(p => !string.IsNullOrWhiteSpace(p.Uuid)).ToDictionary(p => p.Uuid, StringComparer.OrdinalIgnoreCase);

            phase2bSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 2b (Hydrate and aggregate): {phase2bSw.ElapsedMilliseconds} ms");

            // PHASE 3a, 3b in parallel
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
                        throw new InvalidOperationException($"Cannot materialize collection card. Printing not found for UUID '{row.Identity.Uuid}'.");
                    }

                    return CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                }).ToList();

                myCollectionVM.Cards = SortCards(myCollection);
                myCollectionVM.FilteredCards = myCollectionVM.Cards;

                return myCollection;
            });

            var oracleCardsTask = Task.Run(() =>
            {
                var oracleCards = aggregatedPrintings
                    .Select(p => p.Oracle)
                    .Where(o => !string.IsNullOrWhiteSpace(o.ScryfallOracleId))
                    .GroupBy(o => o.ScryfallOracleId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                oracleCardsVM.Cards = SortOracleCards(oracleCards);
                oracleCardsVM.FilteredCards = oracleCardsVM.Cards;

                return oracleCards;
            });

            await Task.WhenAll(allCardsTask, myCollectionTask, oracleCardsTask);

            phase3abSw.Stop();
            Debug.WriteLine($"[InitializeCardListsAsync] phase 3a and 3b (build AllCards, MyCollection, and OracleCards objects): {phase3abSw.ElapsedMilliseconds} ms");

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


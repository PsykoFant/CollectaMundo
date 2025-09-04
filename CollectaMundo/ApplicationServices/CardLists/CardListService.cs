using CollectaMundo.ApplicationServices.CardLists.Lookups;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.ViewModels;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(ICardListRepository cardListRepo, IFilterDefaultsLogic filterLogic, ICardLookupsService lookupService, ICardCoreAggregator aggregator) : ICardListService
    {
        private readonly ICardListRepository _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterLogic = filterLogic;
        private readonly ICardLookupsService _lookupService = lookupService;
        private readonly ICardCoreAggregator _aggregator = aggregator;
        public async Task InitializeCardListsAsync(
            CardViewModel allCardsVM,
            CardViewModel myCollectionVM,
            Dictionary<string, FilterItemViewModel> filters,
            FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork();
            try
            {
                // use read-only for this whole startup pass
                await uow.BeginReadOnlyAsync();
                var conn = uow.CurrentConnection;

                // Phase 1: DB I/O only (in order)
                var lookupPackage = await _lookupService.LoadLookupDataAsync(conn, CardLookupsOptions.All);
                var coreDtos = await _cardListRepo.ReadAllCardsCoreDtosAsync(conn);
                var collectionRows = await _cardListRepo.ReadMyCollectionAsync(conn);
                await uow.CommitAsync(); // DB done

                // Phase 2a: Assign static providers (must be done before CardSet.FromCore)
                CardSet.ManaCostImages = lookupPackage.ManaCostImages;
                CardSet.SetIconImages = lookupPackage.SetIconImages;
                CardSet.SetMetaProvider = lookupPackage.SetMetaProvider;
                CardSet.PriceMetaProvider = lookupPackage.PriceMetaProvider;

                // Phase 2b: Hydrate + aggregate
                var cores = coreDtos
                    .AsParallel()
                    .AsOrdered()
                    .Select(CardCore.FromDto) // ✨ domain logic hydration here
                    .ToList();

                var aggregatedCores = _aggregator.Aggregate(cores);

                var byUuid = aggregatedCores
                    .ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

                // Phase 3a: Build AllCards
                var allCards = aggregatedCores
                    .AsParallel()
                    .AsOrdered()
                    .Select(CardSet.FromCore)
                    .ToList();

                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCards;

                // Phase 3b: Build MyCollection
                var myCollection = collectionRows
                    .AsParallel()
                    .Select(r =>
                        byUuid.TryGetValue(r.Uuid, out var core)
                            ? CardSet.FromCoreWithCollection(core, r.Id, r.CardsOwned, r.CardsForTrade, r.Condition, r.Language, r.Finish)
                            : null)
                    .Where(c => c is not null)
                    .Cast<CardSet>()
                    .ToList();

                myCollectionVM.Cards = myCollection;
                myCollectionVM.FilteredCards = myCollection;

                // Phase 3c: Build Filters
                var defs = _filterLogic.Build(allCards, myCollection);
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
    }
}


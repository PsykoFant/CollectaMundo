using CollectaMundo.ApplicationServices.CardIcons;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.ViewModels;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(ICardListRepository cardListRepo, IFilterDefaultsLogic filterRepo, ICardIconsService iconService) : ICardListService
    {
        private readonly ICardListRepository _cardListRepo = cardListRepo;
        private readonly IFilterDefaultsLogic _filterRepo = filterRepo;
        private readonly ICardIconsService _iconService = iconService;
        public async Task InitializeAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork();
            try
            {
                // use read-only for this whole startup pass
                await uow.BeginReadOnlyAsync();
                var conn = uow.CurrentConnection;

                // Ensure icon providers using the SAME connection (no parallel connection/txn)
                await _iconService.InitializeAsync(conn);

                // 1) AllCards cores
                var cores = await _cardListRepo.ReadAllCardsCoresAsync(conn);
                var byUuid = new Dictionary<string, CardCore>(cores.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var core in cores)
                {
                    byUuid[core.Uuid] = core;
                }

                var allCards = cores.AsParallel().AsOrdered().Select(CardSet.FromCore).ToList();
                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCards;

                // 2) MyCollection
                var rows = await _cardListRepo.ReadMyCollectionAsync(conn);
                var myCollection = rows.AsParallel()
                    .Select(r => byUuid.TryGetValue(r.Uuid, out var core)
                        ? CardSet.FromCoreWithCollection(core, r.Id, r.CardsOwned, r.CardsForTrade, r.Condition, r.Language, r.Finish)
                        : null)
                    .Where(x => x is not null)
                    .Cast<CardSet>()
                    .ToList();

                myCollectionVM.Cards = myCollection;
                myCollectionVM.FilteredCards = myCollection;

                // 3) Filters
                var defs = _filterRepo.Build(allCardsVM.Cards, myCollectionVM.Cards);
                filters.Clear();
                foreach (var def in defs)
                {
                    filters[def.CriteriaKey] = new FilterItemViewModel(
                        def.CriteriaKey, def.FilterOptions, def.DefaultText, def.ReadableLabel, filterVM, def.NumericCriteria);
                }

                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
    }
}


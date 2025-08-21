using CollectaMundo.ApplicationServices.CardIcons;
using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardIcons;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(ICardListRepository cardListRepo, IFilterInitDefaultsRepository filterRepo, ICardIconsService iconService) : ICardListService
    {
        private readonly ICardListRepository _cardListRepo = cardListRepo;
        private readonly IFilterInitDefaultsRepository _filterRepo = filterRepo;
        private readonly ICardIconsService _iconService = iconService;

        // Lazy singletons created on first InitializeAsync
        private IImageBytesLogic<string>? _manaBytes;            // Domain: key -> bytes
        private IImageProvider<string>? _manaImages;          // AppSvc: key -> ImageSource

        public async Task InitializeAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            // Ensure icon providers are ready (no-op after first call)
            await _iconService.InitializeAsync();

            await using var uow = new UnitOfWork();
            try
            {
                await uow.BeginAsync();
                var conn = uow.CurrentConnection;



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
                var defs = await _filterRepo.GetFilterDefaultsAsync(conn);
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


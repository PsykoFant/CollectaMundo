using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Images;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;


namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardListService(ICardListRepository cardListRepo, IFilterInitDefaultsRepository filterRepo) : ICardListService
    {
        private readonly ICardListRepository _cardListRepo = cardListRepo;
        private readonly IFilterInitDefaultsRepository _filterRepo = filterRepo;

        // Lazily built once on the first InitializeAsync call
        private IManaCostImageProvider? _manaProvider;

        public async Task InitializeAsync(CardViewModel allCardsVM, CardViewModel myCollectionVM, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork();
            try
            {
                await uow.BeginAsync();
                var conn = uow.CurrentConnection;

                // --- Ensure mana provider is built exactly once ---
                if (_manaProvider is null)
                {
                    var map = await _cardListRepo.ReadManaCostImagesAsync(conn);
                    _manaProvider = new ManaCostImageCache(map);
                }

                // Hook provider (idempotent; safe if already set)
                CardSet.ManaCostImageProvider = _manaProvider;

                // 1) Load AllCards cores
                Debug.WriteLine("[CardListService] Loading AllCards cores…");
                var cores = await _cardListRepo.ReadAllCardsCoresAsync(conn);

                // 2) Index by UUID
                var byUuid = new Dictionary<string, CardCore>(cores.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var core in cores)
                    byUuid[core.Uuid] = core;

                // 3) Project AllCards
                var allCards = cores
                    .AsParallel()
                    .AsOrdered()
                    .Select(CardSet.FromCore)
                    .ToList();

                allCardsVM.Cards = allCards;
                allCardsVM.FilteredCards = allCards;

                // 4) Load myCollection and join in memory
                Debug.WriteLine("[CardListService] Loading myCollection…");
                var rows = await _cardListRepo.ReadMyCollectionAsync(conn);

                var myCollection = rows
                    .AsParallel()
                    .Select(r => byUuid.TryGetValue(r.Uuid, out var core)
                        ? CardSet.FromCoreWithCollection(core, r.Id, r.CardsOwned, r.CardsForTrade, r.Condition, r.Language, r.Finish)
                        : null)
                    .Where(c => c is not null)
                    .Cast<CardSet>()
                    .ToList();

                myCollectionVM.Cards = myCollection;
                myCollectionVM.FilteredCards = myCollection;

                // 5) Filter defaults
                var defs = await _filterRepo.GetFilterDefaultsAsync(conn);
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

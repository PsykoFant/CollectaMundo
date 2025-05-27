using CollectaMundo.Data;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public class MainWindowInitializer(IDbConnectionFactory factory)
    {
        public async Task InitializeAsync(
            List<(CardViewModel, CardListQuerySpec)> cardSpecs,
            Dictionary<string, FilterItemViewModel> filters,
            FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork(factory);
            await uow.BeginAsync();

            var repo = new CardListRepository(uow.CurrentConnection);
            var filterRepo = new FilterInitDefaultsRepository(uow.CurrentConnection);

            var cardTasks = cardSpecs
                .Select(s => repo.QueryAsync(s.Item2.Sql, s.Item2.Mapper))
                .ToArray();

            var cardsResults = await Task.WhenAll(cardTasks);
            for (int i = 0; i < cardSpecs.Count; i++)
            {
                cardSpecs[i].Item1.Cards = cardsResults[i].ToList();
            }

            var filterDefaults = await filterRepo.GetFilterDefaultsAsync();
            filters.Clear();
            foreach (var def in filterDefaults)
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
    }

}

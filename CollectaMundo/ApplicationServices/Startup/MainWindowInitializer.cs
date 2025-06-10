using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.Startup
{
    public class MainWindowInitializer(IDbConnectionFactory factory)
    {
        public async Task InitializeAsync(List<(CardViewModel, CardListQuerySpec)> cardSpecs, Dictionary<string, FilterItemViewModel> filters, FilterViewModel filterVM)
        {
            await using var uow = new UnitOfWork(factory);
            await uow.BeginAsync();
            var conn = uow.CurrentConnection;

            var cardlistRepo = new CardListRepository();
            var filterRepo = new FilterInitDefaultsRepository();

            var cardTasks = cardSpecs
                .Select(s => cardlistRepo.QueryAsync(s.Item2.Sql, conn, s.Item2.Mapper))
                .ToArray();

            var cardsResults = await Task.WhenAll(cardTasks);
            for (int i = 0; i < cardSpecs.Count; i++)
            {
                cardSpecs[i].Item1.Cards = [.. cardsResults[i]];
            }

            var filterDefaults = await filterRepo.GetFilterDefaultsAsync(conn);
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

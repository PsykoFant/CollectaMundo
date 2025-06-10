using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public class CardListInitService(IUnitOfWork uow) : ICardListInitService
    {
        private readonly IUnitOfWork _uow = uow ?? throw new ArgumentNullException(nameof(uow));

        public async Task LoadCardListsAsync(List<(CardViewModel target, CardListQuerySpec spec)> specs)
        {
            await _uow.BeginAsync();
            try
            {
                var repo = new CardListRepository();
                var tasks = specs.Select(s => repo.QueryAsync(s.spec.Sql, _uow.CurrentConnection, s.spec.Mapper)).ToArray();
                var results = await Task.WhenAll(tasks);

                for (int i = 0; i < specs.Count; i++)
                {
                    specs[i].target.Cards = [.. results[i]];
                }
            }
            finally
            {
                await _uow.DisposeAsync();
            }
        }

    }


}



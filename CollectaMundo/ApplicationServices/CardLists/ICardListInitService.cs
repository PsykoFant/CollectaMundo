using CollectaMundo.Data.CardLists;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardListInitService
    {
        Task LoadCardListsAsync(List<(CardViewModel target, CardListQuerySpec spec)> specs);
    }
}

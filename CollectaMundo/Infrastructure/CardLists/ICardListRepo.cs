using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public interface ICardListRepo
    {
        Task<IReadOnlyList<CardPrintingDbRow>> ReadAllCardPrintingDbRowsAsync(SQLiteConnection conn);
        Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn);
    }

}

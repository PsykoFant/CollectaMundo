using CollectaMundo.Infrastructure.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public interface ICardListRepo
    {
        Task<IReadOnlyList<PrintingCardDbRow>> ReadAllCardPrintingDbRowsAsync(SQLiteConnection conn);
        Task<List<CollectionCardDbRow>> ReadMyCollectionAsync(SQLiteConnection conn);
    }

}

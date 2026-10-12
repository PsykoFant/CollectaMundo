using CollectaMundo.Infrastructure.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public interface ICardListRepo
    {
        Task<IReadOnlyList<CanonicalOracleFaceDbRow>> ReadCanonicalOracleFacesAsync(SQLiteConnection conn);
        Task<IReadOnlyList<PrintingCardDbRow>> ReadPrintingCardRowsAsync(SQLiteConnection conn);
        Task<List<CollectionCardDbRow>> ReadMyCollectionAsync(SQLiteConnection conn);
    }

}

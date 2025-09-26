using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public interface ICardLookupsRepo
    {
        Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, byte[]>> ReadSetIconImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, SetDto>> ReadSetsAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, PriceDto>> ReadPricesAsync(SQLiteConnection conn, string retailer, string format = "paper");
    }
}

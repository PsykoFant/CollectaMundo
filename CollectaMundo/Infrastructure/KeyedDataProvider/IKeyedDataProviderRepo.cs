using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.KeyedDataProvider
{
    public interface IKeyedDataProviderRepo
    {
        Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, byte[]>> ReadSetIconImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, SetDto>> ReadSetsAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, PriceDto>> ReadPricesAsync(SQLiteConnection conn, string retailer, string format = "paper");
        Task<IReadOnlyDictionary<int, CardLocation>> ReadLocationsAsync(SQLiteConnection conn);
    }
}

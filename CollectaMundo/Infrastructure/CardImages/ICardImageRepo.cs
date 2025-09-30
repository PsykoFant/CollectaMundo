using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardImages
{
    public interface ICardImageRepo
    {
        Task<string?> GetScryfallIdByUuidAsync(string uuid, SQLiteConnection conn);
        Task<string?> GetImagePromoTypeByUuidAsync(string uuid, SQLiteConnection conn);
        Task<string[]> GetScryfallIdByNameAsync(string name, SQLiteConnection conn);
        Task<string?> GetOtherFaceScryfallIdByUuidAsync(string uuid, SQLiteConnection conn);
    }
}

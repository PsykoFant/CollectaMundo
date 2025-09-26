using System.Data.SQLite;

namespace CollectaMundo.Data.CardImages
{
    public interface ICardImageRepo
    {
        Task<string?> GetScryfallIdByUuidAsync(string uuid, SQLiteConnection conn);
    }
}

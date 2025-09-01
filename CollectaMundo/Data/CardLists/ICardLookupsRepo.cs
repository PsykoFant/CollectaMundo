using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public interface ICardLookupsRepo
    {
        Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, byte[]>> ReadSetIconImagesAsync(SQLiteConnection conn);
    }
}

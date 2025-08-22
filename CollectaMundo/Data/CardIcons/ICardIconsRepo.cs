using System.Data.SQLite;

namespace CollectaMundo.Data.CardIcons
{
    public interface ICardIconsRepo
    {
        Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, byte[]>> ReadSetIconImagesAsync(SQLiteConnection conn);
    }
}

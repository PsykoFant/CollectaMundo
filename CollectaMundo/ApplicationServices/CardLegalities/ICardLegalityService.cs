using CollectaMundo.DomainLogic.CardLegalities;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLegalities
{
    public interface ICardLegalityService
    {
        IReadOnlyList<CardLegalityFormat> Formats { get; }
        CardLegalityFormat? GetFormat(string? value);
        Task LoadFormatsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyDictionary<string, CardLegalityMasks>> PrepareDerivedDataAsync(SQLiteConnection conn, SQLiteTransaction tx);
    }
}

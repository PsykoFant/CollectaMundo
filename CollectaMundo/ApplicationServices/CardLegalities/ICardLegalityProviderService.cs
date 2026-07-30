using CollectaMundo.DomainLogic.CardLegalities;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLegalities
{
    public interface ICardLegalityProviderService
    {
        Task LoadAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        IReadOnlyList<CardLegalityFormat> Formats { get; }
        IReadOnlyDictionary<string, CardLegalityMasks> MasksByUuid { get; }
        CardLegalityFormat? GetFormat(string? format);
    }
}

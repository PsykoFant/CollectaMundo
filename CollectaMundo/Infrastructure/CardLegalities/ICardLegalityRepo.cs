using CollectaMundo.Infrastructure.CardLegalities.Models.CollectaMundo.Infrastructure.CardLegalities.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLegalities
{
    public interface ICardLegalityRepo
    {
        Task<IReadOnlyList<CardLegalityDbRow>> GetAllAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
    }
}

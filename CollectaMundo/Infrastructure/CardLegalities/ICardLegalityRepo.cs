using CollectaMundo.Infrastructure.CardLegalities.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLegalities
{
    public interface ICardLegalityRepo
    {
        Task<CardLegalityReadResult> GetAllAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
    }
}

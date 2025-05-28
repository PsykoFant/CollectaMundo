using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;

namespace CollectaMundo.Data
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardSet>> QueryAsync(string sql, Func<DbDataReader, CardSet> map);
    }

}

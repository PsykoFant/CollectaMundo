using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;

namespace CollectaMundo.Data
{
    public class CardListQuerySpec
    {
        public required string Sql { get; init; }
        public required Func<DbDataReader, CardSet> Mapper { get; init; }
    }

}

using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public class CardListRepository : ICardListRepository
    {
        public Task<IReadOnlyList<CardSet>> GetAllCardsAsync() =>
            MapAsync(new SQLiteCommand("select * from view_allCards", DBAccess.connection),
              reader => CardFactory.FromAllCardsRow(reader));

        public Task<IReadOnlyList<CardSet>> GetMyCollectionAsync() =>
            MapAsync(new SQLiteCommand("select * from view_myCollection", DBAccess.connection),
                reader => CardFactory.FromMyCollectionRow(reader));

        private static async Task<IReadOnlyList<CardSet>> MapAsync(SQLiteCommand cmd, Func<DbDataReader, CardSet> mapRow)
        {
            var cards = new List<CardSet>();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                cards.Add(mapRow(rdr));
            return cards;
        }


    }
}

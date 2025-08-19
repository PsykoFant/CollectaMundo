using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data.CardLists
{
    public class CardListRepository() : ICardListRepository
    {
        public async Task<IReadOnlyList<CardCore>> QueryAllCardsCoresAsync(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("SELECT * FROM view_allCards", conn);
            var list = new List<CardCore>(capacity: 120000);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(CardListMapper.CoreFromAllCardsRow(reader));
            }
            return list;
        }
        public async Task<List<MyCollectionRow>> ReadMyCollection(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("SELECT id, uuid, cardsOwned, cardsForTrade, condition, language, finish FROM myCollection", conn);

            var list = new List<MyCollectionRow>();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new MyCollectionRow
                {
                    Id = rdr["id"] is long li ? (int)li : (int)(rdr["id"] ?? 0),
                    Uuid = rdr["uuid"]?.ToString() ?? "",
                    CardsOwned = rdr["cardsOwned"] is long lo ? (int)lo : (int)(rdr["cardsOwned"] ?? 0),
                    CardsForTrade = rdr["cardsForTrade"] is long lt ? (int)lt : (int)(rdr["cardsForTrade"] ?? 0),
                    Condition = rdr["condition"]?.ToString(),
                    Language = rdr["language"]?.ToString(),
                    Finish = rdr["finish"]?.ToString()
                });
            }
            return list;
        }
        public Task<IReadOnlyList<CardSet>> QueryAsync(string sql, SQLiteConnection conn, Func<DbDataReader, CardSet> map) => MapAsync(new SQLiteCommand(sql, conn), map);
        private static async Task<IReadOnlyList<CardSet>> MapAsync(SQLiteCommand cmd, Func<DbDataReader, CardSet> mapRow)
        {
            try
            {
                var cards = new List<CardSet>();
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    cards.Add(mapRow(rdr));
                }

                Debug.WriteLine($"Loaded {cards.Count} cards");
                return cards;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MapAsync: {ex.Message}");
                return [];
            }
        }
    }
}

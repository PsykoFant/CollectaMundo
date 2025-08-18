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

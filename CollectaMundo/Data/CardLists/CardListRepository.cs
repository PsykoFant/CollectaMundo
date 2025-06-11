using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data.CardLists
{
    public class CardListRepository() : ICardListRepository
    {
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

using CollectaMundo.DomainLogic.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class CardListRepository(SQLiteConnection connection) : ICardListRepository
    {
        private readonly SQLiteConnection _connection = connection;

        public Task<IReadOnlyList<CardSet>> QueryAsync(string sql, Func<DbDataReader, CardSet> map) => MapAsync(new SQLiteCommand(sql, _connection), map);

        private static async Task<IReadOnlyList<CardSet>> MapAsync(SQLiteCommand cmd, Func<DbDataReader, CardSet> mapRow)
        {
            try
            {
                var cards = new List<CardSet>();
                using var rdr = await cmd.ExecuteReaderAsync();
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

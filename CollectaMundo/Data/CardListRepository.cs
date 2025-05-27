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
    //public class CardListRepository(SQLiteConnection connection) : ICardListRepository
    //{
    //    private readonly SQLiteConnection _connection = connection;
    //    public Task<IReadOnlyList<CardSet>> GetAllCardsAsync() => MapAsync(new SQLiteCommand("select * from view_allCards", _connection), reader => CardListMapper.FromAllCardsRow(reader));
    //    public Task<IReadOnlyList<CardSet>> GetMyCollectionAsync() => MapAsync(new SQLiteCommand("select * from view_myCollection", _connection), reader => CardListMapper.FromMyCollectionRow(reader));
    //    public Task<IReadOnlyList<CardSet>> GetCardsForDecksAsync() => MapAsync(new SQLiteCommand("select * from view_allCardsForDecks", _connection), reader => CardListMapper.FromAllCardsForDecksRow(reader));
    //    public Task<IReadOnlyList<CardSet>> GetCardsInDecksAsync() => MapAsync(new SQLiteCommand("select * from view_cardsInDecks", _connection), reader => CardListMapper.FromAllCardsInDecksRow(reader));

    //    private static readonly string colorIconsQuery = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";
    //    public Task<IReadOnlyList<CardSet>> GetColorIconsAsync() => MapAsync(new SQLiteCommand(colorIconsQuery, _connection), reader => CardListMapper.FromColorIconsRow(reader));
    //    private static async Task<IReadOnlyList<CardSet>> MapAsync(SQLiteCommand cmd, Func<DbDataReader, CardSet> mapRow)
    //    {
    //        try
    //        {
    //            var cards = new List<CardSet>();
    //            using var rdr = await cmd.ExecuteReaderAsync();
    //            while (await rdr.ReadAsync())
    //            {
    //                cards.Add(mapRow(rdr));
    //            }

    //            Debug.WriteLine($"Loaded {cards.Count} cards");

    //            return cards;
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.WriteLine($"Error in MapAsync: {ex.Message}");
    //            return [];
    //        }
    //    }
    //}
}

using CollectaMundo.Data;
using CollectaMundo.DomainLogic;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public static class CardListQueryCatalog
    {
        public static CardListQuerySpec AllCards => new()
        {
            Sql = "SELECT * FROM view_allCards",
            Mapper = CardListMapper.FromAllCardsRow
        };

        public static CardListQuerySpec MyCollection => new()
        {
            Sql = "SELECT * FROM view_myCollection",
            Mapper = CardListMapper.FromMyCollectionRow
        };
        public static CardListQuerySpec AllCardsForDecks => new()
        {
            Sql = "SELECT * FROM view_allCardsForDecks",
            Mapper = CardListMapper.FromAllCardsForDecksRow
        };
        public static CardListQuerySpec AllCardsInDecks => new()
        {
            Sql = "SELECT * FROM view_cardsInDecks",
            Mapper = CardListMapper.FromAllCardsInDecksRow
        };
        public static CardListQuerySpec ColorIcons => new()
        {
            Sql = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;",
            Mapper = CardListMapper.FromColorIconsRow
        };

        //    public Task<IReadOnlyList<CardSet>> GetAllCardsAsync() => MapAsync(new SQLiteCommand("select * from view_allCards", _connection), reader => CardListMapper.FromAllCardsRow(reader));
        //    public Task<IReadOnlyList<CardSet>> GetMyCollectionAsync() => MapAsync(new SQLiteCommand("select * from view_myCollection", _connection), reader => CardListMapper.FromMyCollectionRow(reader));
        //    public Task<IReadOnlyList<CardSet>> GetCardsForDecksAsync() => MapAsync(new SQLiteCommand("select * from view_allCardsForDecks", _connection), reader => CardListMapper.FromAllCardsForDecksRow(reader));
        //    public Task<IReadOnlyList<CardSet>> GetCardsInDecksAsync() => MapAsync(new SQLiteCommand("select * from view_cardsInDecks", _connection), reader => CardListMapper.FromAllCardsInDecksRow(reader));

        //    private static readonly string colorIconsQuery = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";
        //    public Task<IReadOnlyList<CardSet>> GetColorIconsAsync() => MapAsync(new SQLiteCommand(colorIconsQuery, _connection), reader => CardListMapper.FromColorIconsRow(reader));

        // Add more as needed
    }
}

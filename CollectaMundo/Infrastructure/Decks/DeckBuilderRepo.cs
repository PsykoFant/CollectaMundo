using CollectaMundo.DomainLogic.Decks.Models;
using System.Data;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Decks
{
    public sealed class DeckBuilderRepo : IDeckBuilderRepo
    {
        public async Task<IReadOnlyList<DeckCardEntry>> GetByDeckLocationIdAsync(SQLiteConnection connection, int locationId)
        {
            const string sql = """
                                SELECT
                                    locationId,
                                    oracleId,
                                    cardName,
                                    desiredQuantity,
                                    section
                                FROM myDeckCards
                                WHERE locationId = @locationId
                                ORDER BY
                                    CASE section
                                        WHEN 'Commander' THEN 0
                                        WHEN 'Companion' THEN 1
                                        WHEN 'Mainboard' THEN 2
                                        WHEN 'Sideboard' THEN 3
                                        WHEN 'Maybeboard' THEN 4
                                    END,
                                    cardName COLLATE NOCASE;
                                """;

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@locationId", locationId);

            var results = new List<DeckCardEntry>();

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new DeckCardEntry
                {
                    DeckLocationId = reader.GetInt32(0),
                    OracleId = reader.GetString(1),
                    CardName = reader.GetString(2),
                    DesiredQuantity = reader.GetInt32(3),
                    Section = Enum.Parse<DeckSection>(reader.GetString(4))
                });
            }

            return results;
        }
        public async Task ReplaceDeckAsync(SQLiteConnection connection, SQLiteTransaction transaction, int locationId, IReadOnlyList<DeckCardEntry> entries)
        {
            const string deleteSql = """
                                    DELETE FROM myDeckCards
                                    WHERE locationId = @locationId;
                                    """;

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = deleteSql;
                delete.Parameters.AddWithValue("@locationId", locationId);

                await delete.ExecuteNonQueryAsync();
            }

            const string insertSql = """
                                    INSERT INTO myDeckCards
                                    (
                                        locationId,
                                        oracleId,
                                        cardName,
                                        desiredQuantity,
                                        section
                                    )
                                    VALUES
                                    (
                                        @locationId,
                                        @oracleId,
                                        @cardName,
                                        @desiredQuantity,
                                        @section
                                    );
                                    """;

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = insertSql;

            var pLocationId = insert.Parameters.Add("@locationId", DbType.Int32);
            var pOracleId = insert.Parameters.Add("@oracleId", DbType.String);
            var pCardName = insert.Parameters.Add("@cardName", DbType.String);
            var pQuantity = insert.Parameters.Add("@desiredQuantity", DbType.Int32);
            var pSection = insert.Parameters.Add("@section", DbType.String);

            foreach (var entry in entries.Where(x => x.DesiredQuantity > 0))
            {
                pLocationId.Value = locationId;
                pOracleId.Value = entry.OracleId;
                pCardName.Value = entry.CardName;
                pQuantity.Value = entry.DesiredQuantity;
                pSection.Value = entry.Section.ToString();

                await insert.ExecuteNonQueryAsync();
            }
        }
    }
}

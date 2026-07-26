using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
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
                                        ELSE 5
                                    END,
                                    cardName COLLATE NOCASE;
                                """;

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add("@locationId", DbType.Int32).Value = locationId;

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
                    Section = Enum.Parse<DeckSection>(reader.GetString(4), ignoreCase: true)
                });
            }

            return results;
        }
        public async Task ReplaceDeckAsync(SQLiteConnection connection, SQLiteTransaction transaction, int locationId, IReadOnlyCollection<DeckCardEntry> entries)
        {
            await DeleteDeckEntriesAsync(connection, transaction, locationId);
            await InsertDeckEntriesAsync(connection, transaction, locationId, entries);
        }
        private static async Task DeleteDeckEntriesAsync(SQLiteConnection connection, SQLiteTransaction transaction, int locationId)
        {
            const string sql = """
                                DELETE FROM myDeckCards
                                WHERE locationId = @locationId;
                                """;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            command.Parameters.Add("@locationId", DbType.Int32).Value = locationId;

            await command.ExecuteNonQueryAsync();
        }
        private static async Task InsertDeckEntriesAsync(SQLiteConnection connection, SQLiteTransaction transaction, int locationId, IReadOnlyCollection<DeckCardEntry> entries)
        {
            const string sql = """
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

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            var locationParameter = command.Parameters.Add("@locationId", DbType.Int32);
            var oracleIdParameter = command.Parameters.Add("@oracleId", DbType.String);
            var cardNameParameter = command.Parameters.Add("@cardName", DbType.String);
            var quantityParameter = command.Parameters.Add("@desiredQuantity", DbType.Int32);
            var sectionParameter = command.Parameters.Add("@section", DbType.String);

            foreach (var entry in entries)
            {
                if (entry.DesiredQuantity <= 0)
                {
                    continue;
                }

                locationParameter.Value = locationId;
                oracleIdParameter.Value = entry.OracleId;
                cardNameParameter.Value = entry.CardName;
                quantityParameter.Value = entry.DesiredQuantity;
                sectionParameter.Value = entry.Section.ToString();

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

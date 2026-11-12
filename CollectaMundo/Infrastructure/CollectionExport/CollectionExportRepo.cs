using CollectaMundo.Infrastructure.CollectionExport.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Globalization;

namespace CollectaMundo.Infrastructure.CollectionExport
{
    public class CollectionExportRepo() : ICollectionExportRepo
    {
        public async Task<IReadOnlyList<CollectionTradeExportItem>> GetTradeListItemsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default)
        {
            const string sql = """
                                SELECT
                                    collection.uuid AS uuid,
                                    identifiers.mcmId AS mcmId,
                                    identifiers.scryfallId AS scryfallId,
                                    identifiers.tcgplayerProductId AS tcgplayerProductId,
                                    collection.cardsForTrade AS cardsForTrade,
                                    card.name AS name,
                                    card.setName AS setName,
                                    collection.condition AS condition,
                                    collection.language AS language,
                                    collection.finish AS finish,
                                    collection.sellPrice AS sellPrice,
                                    collection.sellPriceCurrency AS sellPriceCurrency,
                                    collection.comment AS comment
                                FROM myCollection collection
                                INNER JOIN view_cardToken card
                                    ON card.uuid = collection.uuid
                                LEFT JOIN cardIdentifiers identifiers
                                    ON identifiers.uuid = collection.uuid
                                WHERE collection.cardsForTrade > 0
                                ORDER BY card.name COLLATE NOCASE, card.setName COLLATE NOCASE;
                                """;

            using var command = new SQLiteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var rows = new List<CollectionTradeExportItem>();

            var uuidOrdinal = reader.GetOrdinal("uuid");
            var mcmIdOrdinal = reader.GetOrdinal("mcmId");
            var scryfallIdOrdinal = reader.GetOrdinal("scryfallId");
            var tcgplayerProductIdOrdinal = reader.GetOrdinal("tcgplayerProductId");
            var cardsForTradeOrdinal = reader.GetOrdinal("cardsForTrade");
            var nameOrdinal = reader.GetOrdinal("name");
            var setNameOrdinal = reader.GetOrdinal("setName");
            var conditionOrdinal = reader.GetOrdinal("condition");
            var languageOrdinal = reader.GetOrdinal("language");
            var finishOrdinal = reader.GetOrdinal("finish");
            var sellPriceOrdinal = reader.GetOrdinal("sellPrice");
            var sellPriceCurrencyOrdinal = reader.GetOrdinal("sellPriceCurrency");
            var commentOrdinal = reader.GetOrdinal("comment");

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CollectionTradeExportItem
                {
                    Uuid = reader.GetString(uuidOrdinal),
                    McmId = GetNullableString(reader, mcmIdOrdinal),
                    ScryfallId = GetNullableString(reader, scryfallIdOrdinal),
                    TcgplayerProductId = GetNullableString(reader, tcgplayerProductIdOrdinal),
                    CardsForTrade = reader.GetInt32(cardsForTradeOrdinal),
                    Name = reader.GetString(nameOrdinal),
                    SetName = reader.GetString(setNameOrdinal),
                    Condition = reader.GetString(conditionOrdinal),
                    Language = reader.GetString(languageOrdinal),
                    Finish = reader.GetString(finishOrdinal),
                    SellPrice = reader.IsDBNull(sellPriceOrdinal)
                        ? null
                        : Convert.ToDecimal(reader.GetValue(sellPriceOrdinal), CultureInfo.InvariantCulture),
                    SellPriceCurrency = GetNullableString(reader, sellPriceCurrencyOrdinal),
                    Comment = GetNullableString(reader, commentOrdinal)
                });
            }

            return rows;
        }
        private static string? GetNullableString(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        public async Task<IReadOnlyList<CollectionBackupRow>> GetFullCollectionRowsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default)
        {
            const string sql = """
                            SELECT id,
                                   uuid,
                                   condition,
                                   finish,
                                   language,
                                   locationId,
                                   comment,
                                   cardsOwned,
                                   cardsForTrade,
                                   sellPrice,
                                   sellPriceCurrency
                            FROM myCollection;
                            """;

            using var command = new SQLiteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var rows = new List<CollectionBackupRow>();

            var idOrdinal = reader.GetOrdinal("id");
            var uuidOrdinal = reader.GetOrdinal("uuid");
            var conditionOrdinal = reader.GetOrdinal("condition");
            var finishOrdinal = reader.GetOrdinal("finish");
            var languageOrdinal = reader.GetOrdinal("language");
            var locationIdOrdinal = reader.GetOrdinal("locationId");
            var commentOrdinal = reader.GetOrdinal("comment");
            var cardsOwnedOrdinal = reader.GetOrdinal("cardsOwned");
            var cardsForTradeOrdinal = reader.GetOrdinal("cardsForTrade");
            var sellPriceOrdinal = reader.GetOrdinal("sellPrice");
            var sellPriceCurrencyOrdinal = reader.GetOrdinal("sellPriceCurrency");

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CollectionBackupRow
                {
                    Id = reader.GetInt32(idOrdinal),
                    Uuid = reader.GetString(uuidOrdinal),
                    Condition = reader.GetString(conditionOrdinal),
                    Finish = reader.GetString(finishOrdinal),
                    Language = reader.GetString(languageOrdinal),
                    LocationId = reader.IsDBNull(locationIdOrdinal)
                        ? null
                        : reader.GetInt32(locationIdOrdinal),
                    Comment = reader.IsDBNull(commentOrdinal)
                        ? null
                        : reader.GetString(commentOrdinal),
                    CardsOwned = reader.GetInt32(cardsOwnedOrdinal),
                    CardsForTrade = reader.GetInt32(cardsForTradeOrdinal),
                    SellPrice = reader.IsDBNull(sellPriceOrdinal)
                        ? null
                        : Convert.ToDecimal(
                            reader.GetValue(sellPriceOrdinal),
                            CultureInfo.InvariantCulture),
                    SellPriceCurrency = reader.IsDBNull(sellPriceCurrencyOrdinal)
                        ? null
                        : reader.GetString(sellPriceCurrencyOrdinal)
                });
            }

            return rows;
        }
    }
}

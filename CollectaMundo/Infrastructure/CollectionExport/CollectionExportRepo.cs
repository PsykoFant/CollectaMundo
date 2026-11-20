using CollectaMundo.Infrastructure.CollectionExport.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Globalization;

namespace CollectaMundo.Infrastructure.CollectionExport
{
    public class CollectionExportRepo() : ICollectionExportRepo
    {
        public async Task<IReadOnlyList<CollectionBackupRow>> GetFullCollectionRowsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default)
        {
            const string sql = """
                                WITH collectionItems AS (
                                    SELECT
                                        card.name AS name,
                                        card.setCode AS setCode,
                                        collection.uuid AS uuid,
                                        identifiers.scryfallId AS scryfallId,
                                        collection.condition AS condition,
                                        collection.finish AS finish,
                                        collection.language AS language,
                                        collection.comment AS comment,
                                        collection.locationId AS locationId,
                                        collection.cardsOwned AS cardsOwned,
                                        collection.cardsForTrade AS cardsForTrade,
                                        collection.sellPrice AS sellPrice,
                                        collection.sellPriceCurrency AS sellPriceCurrency
                                    FROM myCollection collection
                                    INNER JOIN cards card
                                        ON card.uuid = collection.uuid
                                       AND (card.side IS NULL OR card.side = 'a')
                                    LEFT JOIN cardIdentifiers identifiers
                                        ON identifiers.uuid = collection.uuid

                                    UNION ALL

                                    SELECT
                                        token.name AS name,
                                        token.setCode AS setCode,
                                        collection.uuid AS uuid,
                                        identifiers.scryfallId AS scryfallId,                                       
                                        collection.condition AS condition,
                                        collection.finish AS finish,
                                        collection.language AS language,
                                        collection.comment AS comment,
                                        collection.locationId AS locationId,
                                        collection.cardsOwned AS cardsOwned,
                                        collection.cardsForTrade AS cardsForTrade,
                                        collection.sellPrice AS sellPrice,
                                        collection.sellPriceCurrency AS sellPriceCurrency
                                    FROM myCollection collection
                                    INNER JOIN tokens token
                                        ON token.uuid = collection.uuid
                                       AND (token.side IS NULL OR token.side = 'a')
                                    LEFT JOIN tokenIdentifiers identifiers
                                        ON identifiers.uuid = collection.uuid
                                )
                                SELECT
                                    collectionItems.name,
                                    collectionItems.setCode,
                                    collectionItems.uuid,
                                    collectionItems.condition,
                                    collectionItems.finish,
                                    collectionItems.language,
                                    location.name AS locationName,
                                    location.type AS locationType,
                                    collectionItems.comment,
                                    collectionItems.cardsOwned,
                                    collectionItems.cardsForTrade,
                                    collectionItems.sellPrice,
                                    collectionItems.sellPriceCurrency,
                                    collectionItems.scryfallId
                                FROM collectionItems
                                LEFT JOIN cardLocations location
                                    ON location.id = collectionItems.locationId
                                """;

            using var command = new SQLiteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var rows = new List<CollectionBackupRow>();

            var nameOrdinal = reader.GetOrdinal("name");
            var setCodeOrdinal = reader.GetOrdinal("setCode");
            var uuidOrdinal = reader.GetOrdinal("uuid");
            var scryfallIdOrdinal = reader.GetOrdinal("scryfallId");
            var conditionOrdinal = reader.GetOrdinal("condition");
            var finishOrdinal = reader.GetOrdinal("finish");
            var languageOrdinal = reader.GetOrdinal("language");
            var locationOrdinal = reader.GetOrdinal("locationName");
            var locationTypeOrdinal = reader.GetOrdinal("locationType");
            var commentOrdinal = reader.GetOrdinal("comment");
            var cardsOwnedOrdinal = reader.GetOrdinal("cardsOwned");
            var cardsForTradeOrdinal = reader.GetOrdinal("cardsForTrade");
            var sellPriceOrdinal = reader.GetOrdinal("sellPrice");
            var sellPriceCurrencyOrdinal = reader.GetOrdinal("sellPriceCurrency");

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CollectionBackupRow
                {

                    Name = reader.GetString(nameOrdinal),
                    SetCode = reader.GetString(setCodeOrdinal),
                    Uuid = reader.GetString(uuidOrdinal),
                    ScryfallId = reader.IsDBNull(scryfallIdOrdinal)
                        ? null
                        : reader.GetString(scryfallIdOrdinal),
                    Condition = reader.GetString(conditionOrdinal),
                    Finish = reader.GetString(finishOrdinal),
                    Language = reader.GetString(languageOrdinal),
                    LocationName = reader.IsDBNull(locationOrdinal)
                        ? null
                        : reader.GetString(locationOrdinal),
                    LocationType = reader.IsDBNull(locationTypeOrdinal)
                        ? null
                        : reader.GetString(locationTypeOrdinal),
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
        public async Task<IReadOnlyList<CollectionTradeExportItem>> GetTradeListItemsAsync(SQLiteConnection connection, CancellationToken cancellationToken = default)
        {
            const string sql = """
                                WITH tradeItems AS (
                                    SELECT
                                        collection.uuid AS uuid,
                                        NULL AS tokenScryfallId,
                                        collection.cardsForTrade AS cardsForTrade,
                                        card.name AS name,
                                        cardSet.name AS setName,
                                        collection.condition AS condition,
                                        collection.language AS language,
                                        collection.finish AS finish,
                                        collection.sellPrice AS sellPrice,
                                        collection.sellPriceCurrency AS sellPriceCurrency,
                                        collection.comment AS comment
                                    FROM myCollection collection
                                    INNER JOIN cards card
                                        ON card.uuid = collection.uuid
                                       AND (card.side IS NULL OR card.side = 'a')
                                    INNER JOIN sets cardSet
                                        ON cardSet.code = card.setCode
                                    WHERE collection.cardsForTrade > 0

                                    UNION ALL

                                    SELECT
                                        collection.uuid AS uuid,
                                        tokenIds.scryfallId AS tokenScryfallId,
                                        collection.cardsForTrade AS cardsForTrade,
                                        token.name AS name,
                                        tokenSet.name AS setName,
                                        collection.condition AS condition,
                                        collection.language AS language,
                                        collection.finish AS finish,
                                        collection.sellPrice AS sellPrice,
                                        collection.sellPriceCurrency AS sellPriceCurrency,
                                        collection.comment AS comment
                                    FROM myCollection collection
                                    INNER JOIN tokens token
                                        ON token.uuid = collection.uuid
                                       AND (token.side IS NULL OR token.side = 'a')
                                    INNER JOIN sets tokenSet
                                        ON tokenSet.tokenSetCode = token.setCode
                                    LEFT JOIN tokenIdentifiers tokenIds
                                        ON tokenIds.uuid = token.uuid
                                    WHERE collection.cardsForTrade > 0
                                )
                                SELECT
                                    tradeItems.uuid AS uuid,
                                    identifiers.mcmId AS mcmId,
                                    COALESCE(tradeItems.tokenScryfallId, identifiers.scryfallId) AS scryfallId,
                                    identifiers.tcgplayerProductId AS tcgplayerProductId,
                                    tradeItems.cardsForTrade AS cardsForTrade,
                                    tradeItems.name AS name,
                                    tradeItems.setName AS setName,
                                    tradeItems.condition AS condition,
                                    tradeItems.language AS language,
                                    tradeItems.finish AS finish,
                                    tradeItems.sellPrice AS sellPrice,
                                    tradeItems.sellPriceCurrency AS sellPriceCurrency,
                                    tradeItems.comment AS comment
                                FROM tradeItems
                                LEFT JOIN cardIdentifiers identifiers
                                    ON identifiers.uuid = tradeItems.uuid
                                ORDER BY tradeItems.name COLLATE NOCASE,
                                         tradeItems.setName COLLATE NOCASE;
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
    }
}

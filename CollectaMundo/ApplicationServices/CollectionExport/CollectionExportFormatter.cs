using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.CollectionExport.Models;
using System.Globalization;

namespace CollectaMundo.ApplicationServices.CollectionExport
{
    public static class CollectionExportFormatter
    {
        public static IReadOnlyList<string> BackupHeaders { get; } =
        [
            "name",
            "setCode",
            "uuid",
            "scryfallId",
            "condition",
            "finish",
            "language",
            "location",
            "locationType",
            "comment",
            "cardsOwned",
            "cardsForTrade",
            "sellPrice",
            "sellPriceCurrency"
        ];
        public static IReadOnlyList<string?> ToBackupCsvRow(CollectionBackupRow row)
        {
            var culture = CultureInfo.InvariantCulture;

            return
            [
                row.Name,
                row.SetCode,
                row.Uuid,
                row.ScryfallId,
                row.Condition,
                row.Finish,
                row.Language,
                row.LocationName,
                row.LocationType,
                row.Comment,
                row.CardsOwned.ToString(culture),
                row.CardsForTrade.ToString(culture),
                row.SellPrice?.ToString("0.00", culture),
                row.SellPriceCurrency
            ];
        }
        public static IReadOnlyList<string> TradeListHeaders { get; } =
        [
            "idProduct",
            "quantity",
            "name",
            "set",
            "condition",
            "language",
            "isFoil",
            "price",
            "comment",
            "scryfallId",
            "tcgplayerProductId",
            "uuid"
        ];
        public static IReadOnlyList<string?> ToTradeListCsvRow(CollectionTradeExportItem item, string targetCurrency, decimal usdToEurRate)
        {
            var culture = CultureInfo.InvariantCulture;

            var price = item.SellPrice is decimal sourcePrice
                ? CurrencyConversionLogic.Convert(sourcePrice, item.SellPriceCurrency ?? throw new InvalidOperationException($"Card '{item.Uuid}' has a sell price but no currency."), targetCurrency, usdToEurRate)
                : (decimal?)null;

            return
            [
                item.McmId,
                item.CardsForTrade.ToString(culture),
                item.Name,
                item.SetName,
                ToTradeListCondition(item.Condition),
                item.Language,
                ToTradeListFoil(item.Finish),
                price?.ToString("0.00", culture),
                item.Comment,
                item.ScryfallId,
                item.TcgplayerProductId,
                item.Uuid
            ];
        }
        private static string ToTradeListCondition(string condition)
        {
            return condition.Trim().ToLowerInvariant() switch
            {
                "mint" => "MT",
                "near mint" => "NM",
                "excellent" => "EX",
                "good" => "GD",
                "light played" => "LP",
                "played" => "PL",
                "poor" => "PO",
                _ => throw new InvalidOperationException(
                    $"Unsupported collection condition '{condition}'.")
            };
        }
        private static string ToTradeListFoil(string finish)
        {
            return finish.Trim().ToLowerInvariant() switch
            {
                "nonfoil" => "false",
                "foil" or "etched" => "true",
                _ => throw new InvalidOperationException($"Unsupported card finish '{finish}'.")
            };
        }
    }
}

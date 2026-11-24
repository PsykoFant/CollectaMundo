using CollectaMundo.ApplicationServices.Shared.Files;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.CollectionExport;
using CollectaMundo.Infrastructure.CollectionExport.Models;
using System.Diagnostics;
using System.Globalization;

namespace CollectaMundo.ApplicationServices.CollectionExport
{
    public sealed class CollectionExportService(IAppSettings settings, IUnitOfWorkRunner uowRunner, ICollectionExportRepo repo, ICsvFileWriter csvFileWriter, IDesktopFileSystem desktopFileSystem) : ICollectionExportService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICollectionExportRepo _repo = repo;
        private readonly ICsvFileWriter _csvFileWriter = csvFileWriter;
        private readonly IDesktopFileSystem _desktopFileSystem = desktopFileSystem;

        private static readonly string[] BackupHeaders = ["name", "setCode", "uuid", "scryfallId", "condition", "finish", "language", "location", "locationType", "comment", "cardsOwned", "cardsForTrade", "sellPrice", "sellPriceCurrency"];
        private static readonly string[] TradeListHeaders = ["idProduct", "quantity", "name", "set", "condition", "language", "isFoil", "price", "comment", "scryfallId", "tcgplayerProductId", "uuid"];
        public string TradeListExportCurrency => _settings.TradeListExportCurrency;

        // Persist the selected trade-list export currency in the app settings
        public void PersistTradeListExportCurrency(string currency)
        {
            _settings.PersistTradeListExportCurrency(currency);
        }

        // Export full collection
        public async Task<OperationResult> ExportFullCollectionBackupAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rows = await _uowRunner.ExecuteReadOnlyAsync(
                    connection => _repo.GetFullCollectionRowsAsync(connection, cancellationToken));

                return await WriteFullCollectionBackupAsync(rows, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled backup");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Error creating CSV backup: {ex.Message}");
            }
        }
        private static IReadOnlyList<string?> ToCsvRow(CollectionBackupRow row)
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

        // Export trade list
        public async Task<OperationResult> ExportTradeListAsync(string targetCurrency, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetCurrency))
            {
                return new OperationResult(OperationResultCode.Error, "Select a currency for the trade-list export.");
            }

            var currency = targetCurrency.Trim().ToUpperInvariant();

            if (currency is not ("USD" or "EUR"))
            {
                return new OperationResult(OperationResultCode.Error, $"Unsupported export currency '{targetCurrency}'.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var items = await _uowRunner.ExecuteReadOnlyAsync(
                    connection => _repo.GetTradeListItemsAsync(connection, cancellationToken));

                return await WriteTradeListAsync(items, currency, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "Trade-list export cancelled");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Error creating trade-list export: {ex.Message}");
            }
        }
        private static IReadOnlyList<string?> ToTradeListCsvRow(CollectionTradeExportItem item, string targetCurrency, decimal usdToEurRate)
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
                ToCsvBoolean(IsFoil(item.Finish)),
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
                _ => throw new InvalidOperationException($"Unsupported collection condition '{condition}'.")
            };
        }
        private static bool IsFoil(string finish)
        {
            return finish.Trim().ToLowerInvariant() switch
            {
                "nonfoil" => false,
                "foil" or "etched" => true,
                _ => throw new InvalidOperationException($"Unsupported card finish '{finish}'.")
            };
        }
        private static string ToCsvBoolean(bool value) => value ? "true" : "false";

        // Change backup folder path
        public string BackupFolderPath => _settings.BackupFolderPath;
        public OperationResult ChangeBackupFolderPath()
        {
            try
            {
                var selectedPath = _desktopFileSystem.PickFolder("Select backup folder location", BackupFolderPath);

                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    return new OperationResult(OperationResultCode.CancelledByUser, "No folder selected.");
                }

                _settings.PersistBackupFolderPath(selectedPath);

                return new OperationResult(OperationResultCode.Success, "Folder path changed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing backup folder path: {ex.Message}");

                return new OperationResult(OperationResultCode.Error, $"Error changing backup folder path: {ex.Message}");
            }
        }

        // Open backup folder
        public void OpenBackupFolder()
        {
            try
            {
                _desktopFileSystem.OpenFolder(BackupFolderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening backup folder: {ex.Message}");
                throw;
            }
        }
    }
}

using CollectaMundo.ApplicationServices.Shared.Files;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.Infrastructure.CollectionExport;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CollectionExport
{
    public sealed class CollectionExportService(IAppSettings settings, IUnitOfWorkRunner uowRunner, ICollectionExportRepo repo, ICsvFileWriter csvFileWriter, IDesktopFileSystem desktopFileSystem) : ICollectionExportService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICollectionExportRepo _repo = repo;
        private readonly ICsvFileWriter _csvFileWriter = csvFileWriter;
        private readonly IDesktopFileSystem _desktopFileSystem = desktopFileSystem;
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

                var rows = await _uowRunner.ExecuteReadOnlyAsync(connection => _repo.GetFullCollectionRowsAsync(connection, cancellationToken));

                return await WriteCsvAsync(
                    rows,
                    CollectionExportFormatter.ToBackupCsvRow,
                    CollectionExportFormatter.BackupHeaders,
                    delimiter: ';',
                    fileName: $"CollectaMundoCollection_backup_{DateTime.Now:yyyyMMdd}.csv",
                    emptyMessage: "Your collection is empty - nothing to back up",
                    cancellationToken: cancellationToken
                    );
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

                var items = await _uowRunner.ExecuteReadOnlyAsync(connection => _repo.GetTradeListItemsAsync(connection, cancellationToken));
                var usdToEurRate = _settings.PriceInfo.UsdToEurRate;

                return await WriteCsvAsync(
                    items,
                    item => CollectionExportFormatter.ToTradeListCsvRow(item, currency, usdToEurRate),
                    CollectionExportFormatter.TradeListHeaders,
                    delimiter: ';',
                    fileName: $"MyCollection_trade_list_{currency}_{DateTime.Now:yyyyMMdd}.csv",
                    emptyMessage: "There are no cards marked for trade",
                    cancellationToken: cancellationToken);
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

        // Common CSV writing logic for both full collection backup and trade list export
        private async Task<OperationResult> WriteCsvAsync<TItem>(IReadOnlyList<TItem> items, Func<TItem, IReadOnlyList<string?>> toCsvRow, IReadOnlyList<string> headers, char delimiter, string fileName, string emptyMessage, CancellationToken cancellationToken)
        {
            if (items.Count == 0)
            {
                return new OperationResult(OperationResultCode.Empty, emptyMessage);
            }

            var filePath = Path.Combine(BackupFolderPath, fileName);

            await _csvFileWriter.WriteAsync(filePath, headers, items.Select(toCsvRow), delimiter, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return new OperationResult(OperationResultCode.Success, filePath);
        }

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

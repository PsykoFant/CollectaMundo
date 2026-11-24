using CollectaMundo.ApplicationServices.CollectionExport;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CollectionExportViewModel(ICollectionExportService collectionExportService) : ObservableObject
    {
        private readonly ICollectionExportService _collectionExportService = collectionExportService;
        public void ResetStatusMessage()
        {
            StatusMessage = string.Empty;
        }
        public IReadOnlyList<string> CurrencyOptions { get; } = ["EUR", "USD"];

        [ObservableProperty]
        private string? selectedCurrency = collectionExportService.TradeListExportCurrency;
        partial void OnSelectedCurrencyChanged(string? value)
        {
            if (value is not ("EUR" or "USD"))
            {
                return;
            }

            try
            {
                _collectionExportService.PersistTradeListExportCurrency(value);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not save the export currency: {ex.Message}";
            }
        }

        [ObservableProperty]
        private string backupFolderPath = collectionExportService.BackupFolderPath;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [RelayCommand]
        private Task ExportFullCollectionBackup()
        {
            ResetStatusMessage();
            return RunExportAsync(cancellationToken => _collectionExportService.ExportFullCollectionBackupAsync(cancellationToken));
        }

        [RelayCommand]
        private Task ExportTradeList()
        {
            ResetStatusMessage();

            if (string.IsNullOrWhiteSpace(SelectedCurrency))
            {
                StatusMessage = "Select a currency for the trade-list export.";
                return Task.CompletedTask;
            }

            return RunExportAsync(cancellationToken => _collectionExportService.ExportTradeListAsync(SelectedCurrency, cancellationToken));
        }

        [RelayCommand]
        private void ChangeBackupFolder()
        {
            var result = _collectionExportService.ChangeBackupFolderPath();

            if (result.Code is OperationResultCode.Success)
            {
                BackupFolderPath = _collectionExportService.BackupFolderPath;
            }

            StatusMessage = result.Message;
        }
        private async Task RunExportAsync(Func<CancellationToken, Task<OperationResult>> export)
        {
            IsBusy = true;
            StatusMessage = "Exporting…";

            try
            {
                var result = await export(CancellationToken.None);

                StatusMessage = result.Code == OperationResultCode.Success
                    ? $"Collection successfully exported at: {result.Message}"
                    : result.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            _collectionExportService.OpenBackupFolder();
        }
    }
}

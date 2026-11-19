using CollectaMundo.ApplicationServices.CollectionExport;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CollectionExportViewModel(ICollectionExportService collectionExportService) : ObservableObject
    {
        private readonly ICollectionExportService _collectionExportService = collectionExportService;
        public IReadOnlyList<string> CurrencyOptions { get; } = ["EUR", "USD"];
        public void ResetStatusMessage()
        {
            StatusMessage = string.Empty;
        }

        [ObservableProperty]
        private string? selectedCurrency = "EUR";

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

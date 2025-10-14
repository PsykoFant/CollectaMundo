using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class UpdateViewModel(ICardDatabaseManagementService cardDbManagementService, StatusViewModel statusVM, ParentViewModelContext parentCtx, Func<int> getMyCollectionCount, IFolderPicker folderPicker) : ObservableObject
    {
        private readonly ICardDatabaseManagementService _cardDbManagementService = cardDbManagementService;
        private readonly StatusViewModel _statusVM = statusVM;
        private readonly IUiBlockable _uiState = parentCtx.UiState;
        private readonly IAppRefresher _appRefresher = parentCtx.AppRefresher;
        private readonly Func<int> _getMyCollectionCount = getMyCollectionCount;
        private readonly IFolderPicker _folderPicker = folderPicker;

        // Cancellation tokens
        private CancellationTokenSource? _backupCts;
        private CancellationTokenSource? _checkCts;
        private CancellationTokenSource? _updateCts;

        // Visibility property
        [ObservableProperty]
        private Visibility updateDbVisibility = Visibility.Collapsed;

        // Use case: Backup collection
        [RelayCommand]
        private async Task BackupCollection()
        {
            // UI state preparation
            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Export csv-format backup of My Collection", false);

            _statusVM.StatusLabel3 = $"Export to: {_cardDbManagementService.BackupFolderPath}";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
            _statusVM.SetPrimaryAction(_ =>
            {
                string? selectedPath = _folderPicker.PickFolder("Select backup folder", _cardDbManagementService.BackupFolderPath);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    _cardDbManagementService.ChangeBackupFolderPath(selectedPath);
                    _statusVM.StatusLabel3 = $"Export to: {selectedPath}";
                }
            });
            _statusVM.PrimaryButtonText = "   Change   ";

            _statusVM.SecondaryButtonVisibility = Visibility.Visible;
            _statusVM.SecondaryButtonText = "   Start export   ";

            // Wait for user confirmation before proceeding
            Debug.WriteLine("[DEBUG] BackupCollectionCommand started.");
            _userConfirmedBackup = false;
            _backupTcs = new TaskCompletionSource();
            _statusVM.SetSecondaryAction(_ =>
            {
                Debug.WriteLine("[DEBUG] Secondary action clicked -> completing TCS");
                _userConfirmedBackup = true;
                _backupTcs.SetResult();
            });
            await _backupTcs.Task;
            Debug.WriteLine("[DEBUG] BackupCollectionCommand resumed after TCS completed.");
            _backupTcs = null;

            if (!_userConfirmedBackup)
            {
                Debug.WriteLine("[DEBUG] Backup cancelled by user or superseded by another command.");
                return;
            }

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            _statusVM.ResetStatusOverlay();
            _statusVM.StatusLabel1 = "Please wait - backing up up your collection ... ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Prepare cancellation token before starting
            _backupCts = new CancellationTokenSource();
            _statusVM.SetPrimaryAction(_ =>
            {
                _statusVM.StatusLabel2 = "Cancelling…";
                _backupCts?.Cancel();
            });

            // Run backup
            var result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(_backupCts.Token));

            // Revert primary button to default
            _statusVM.SetPrimaryAction(_ =>
            {
                _statusVM.ResetStatusOverlay();
                _statusVM.HideStatusOverlay();
            });

            // Display result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.StatusLabel3 = $"Backup created successfully at {result.Message}";
                    _statusVM.PrimaryButtonText = "   Awesome!   ";
                    break;

                case OperationResultCode.Empty:
                    _statusVM.StatusLabel3 = "Your collection is empty - nothing to back up";
                    _statusVM.PrimaryButtonText = "   Oh ... I guess that makes sense...   ";
                    break;

                case OperationResultCode.Error:
                    _statusVM.StatusLabel3 = $"Error: {result.Message}";
                    _statusVM.PrimaryButtonText = "   Ok :-/   ";
                    break;
            }

            // Reset UI state
            _backupCts = null;
            SetUiBusy(false);
        }

        // Use case: Check for database updates
        [RelayCommand]
        private async Task CheckForDbUpdates()
        {
            _checkCts = new CancellationTokenSource();

            // UI state preparation
            SetUiBusy(true);
            _statusVM.ResetStatusOverlay();
            _statusVM.PrimaryButtonText = "   Cancel   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
            _statusVM.ShowStatusOverlay("One moment - checking for updates...", false);

            // Hook up cancel action
            _statusVM.SetPrimaryAction(_ =>
            {
                _statusVM.StatusLabel2 = "Cancelling…";
                _checkCts?.Cancel();
            });

            // Run check
            var result = await _cardDbManagementService.CheckForDbUpdatesAsync(_checkCts.Token);

            // Reset UI state
            _statusVM.SetPrimaryAction(null);
            _statusVM.PrimaryButtonText = "   OK   ";
            _checkCts = null;

            switch (result.Code)
            {
                case OperationResultCode.UpToDate:
                    _statusVM.StatusLabel3 = result.Message;
                    break;

                case OperationResultCode.NeedsUpdate:
                    UpdateDbVisibility = Visibility.Visible;
                    _statusVM.StatusLabel3 = result.Message;
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.StatusLabel1 = "Cancelled";
                    _statusVM.StatusLabel3 = "No check was performed.";
                    break;

                default:
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }

            SetUiBusy(false);
        }

        // Use case: Update database
        [RelayCommand]
        protected virtual async Task UpdateDBAsync()
        {
            var skipBackup = _getMyCollectionCount() == 0;
            string backupResultMessage = string.Empty;
            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Ready to update card database?", false);

            if (!skipBackup) { _statusVM.StatusLabel3 = "(we will make a backup of your collection first)"; }

            _statusVM.PrimaryButtonText = "   Go for it!   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Wait for user confirmation before proceeding
            var tcs = new TaskCompletionSource();
            _statusVM.SetPrimaryAction(_ => tcs.SetResult());
            await tcs.Task;

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            UpdateDbVisibility = Visibility.Collapsed;
            _statusVM.PrimaryButtonText = "   Cancel   ";
            _updateCts = new CancellationTokenSource();
            _statusVM.SetPrimaryAction(_ =>
            {
                _statusVM.StatusLabel2 = "Cancelling…";
                _updateCts?.Cancel();
            });


            if (!skipBackup)
            {
                _statusVM.ShowStatusOverlay("Please wait - backing up up your collection ... ", false);
                var backupResult = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(_updateCts.Token));

                if (backupResult.Code is OperationResultCode.CancelledByUser or not OperationResultCode.Success)
                {
                    _statusVM.StatusLabel1 = backupResult.Code == OperationResultCode.CancelledByUser
                        ? "Backup cancelled - aborting update..."
                        : "Backup failed - aborting update...";

                    _statusVM.StatusLabel3 = backupResult.Message;
                    _statusVM.PrimaryButtonVisibility = Visibility.Visible;
                    _statusVM.PrimaryButtonText = "  OK  ";
                    _statusVM.SetPrimaryAction(null);
                    _updateCts = null;
                    return;
                }

                backupResultMessage = backupResult.Message;
            }
            if (_updateCts.IsCancellationRequested)
            {
                _updateCts = null;
                return;
            }

            _statusVM.ShowStatusOverlay("Updating database, please wait...", true);

            // Run the update
            var result = await _cardDbManagementService.UpdateDbPrepOrchetrator(ct: _updateCts.Token);

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.ResetStatusOverlay();
                    _statusVM.StatusLabel3 = "Reloading card lists…";
                    await _appRefresher.ReloadAllCardListsAndFiltersAsync();
                    _statusVM.StatusLabel1 = "Database updated successfully!";
                    if (!skipBackup) { _statusVM.StatusLabel3 = $"Your collection was backed up at {backupResultMessage}!"; }
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.ResetStatusOverlay();
                    _statusVM.StatusLabel1 = "Update canceled";
                    _statusVM.StatusLabel3 = "Download aborted. No files were imported.";
                    break;

                default:
                    _statusVM.ProgressVisibility = Visibility.Collapsed;
                    _statusVM.PrimaryButtonText = "  OK  ";
                    _statusVM.StatusLabel1 = "Card database update failed!";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }

            _updateCts = null;
            SetUiBusy(false);
            _statusVM.SetPrimaryAction(null); // revert to default action (hide overlay)
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
        }

        private TaskCompletionSource? _backupTcs;
        private bool _userConfirmedBackup = false;
        private void CancelPendingBackup()
        {
            if (_backupTcs != null && !_backupTcs.Task.IsCompleted)
            {
                Debug.WriteLine("[DEBUG] Cancelling pending backup TaskCompletionSource...");
                _backupTcs.SetResult(); // ✅ Completes the hanging task
                _backupTcs = null;
                BackupCollectionCommand.NotifyCanExecuteChanged();
            }
        }

        // Use case: Update prices
        [RelayCommand]
        protected virtual async Task UpdatePrices()
        {
            // User bailed from the backup overlay
            CancelPendingBackup();

            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Ready to update card prices?", false);
            _statusVM.PrimaryButtonText = "   Go for it!   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Wait for user confirmation before proceeding
            var tcs = new TaskCompletionSource();
            _statusVM.SetPrimaryAction(_ => tcs.SetResult());
            await tcs.Task;

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            UpdateDbVisibility = Visibility.Collapsed;
            _statusVM.PrimaryButtonText = "   Cancel   ";
            _updateCts = new CancellationTokenSource();
            _statusVM.SetPrimaryAction(_ =>
            {
                _statusVM.StatusLabel2 = "Cancelling…";
                _updateCts?.Cancel();
            });

            if (_updateCts.IsCancellationRequested)
            {
                _updateCts = null;
                return;
            }

            _statusVM.ShowStatusOverlay("Updating card prices, please wait...", true);

            // Run the update
            var result = await _cardDbManagementService.UpdateCardPricesOrchetrator(ct: _updateCts.Token);

            // Reset UI state before showing result            

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.ResetStatusOverlay();
                    _appRefresher.RefreshAllPrices();
                    _statusVM.StatusLabel1 = "Prices updated successfully!";
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.ResetStatusOverlay();
                    _statusVM.StatusLabel1 = "Update canceled";
                    _statusVM.StatusLabel3 = "Download aborted. No prices were updated.";
                    break;

                default:
                    _statusVM.ProgressVisibility = Visibility.Collapsed;
                    _statusVM.PrimaryButtonText = "   OK   ";
                    _statusVM.StatusLabel1 = "Prices update failed!";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }

            _updateCts = null;
            SetUiBusy(false);
            _statusVM.SetPrimaryAction(null); // revert to default action (hide overlay)
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;


        }
        private void SetUiBusy(bool isBusy)
        {
            _uiState.IsTopMenuEnabled = !isBusy;
            _uiState.SideMenuVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
            _uiState.CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}


using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Presentation;
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

        // Visibility property
        [ObservableProperty]
        private Visibility updateDbVisibility = Visibility.Collapsed;

        // Use case: Backup collection
        [RelayCommand]
        private async Task BackupCollection()
        {
            _statusVM.CancelPendingPrompt();
            // Prepare UI
            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Export csv-format backup of My Collection", false);
            _statusVM.StatusLabel3 = $"Export to: {_cardDbManagementService.BackupFolderPath}";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
            _statusVM.SecondaryButtonVisibility = Visibility.Visible;

            // Setup primary
            _statusVM.PrimaryButtonText = "   Change   ";
            _statusVM.SetPrimaryAction(_ =>
            {
                string? selectedPath = _folderPicker.PickFolder("Select backup folder", _cardDbManagementService.BackupFolderPath);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    _cardDbManagementService.ChangeBackupFolderPath(selectedPath);
                    _statusVM.StatusLabel3 = $"Export to: {selectedPath}";
                }
            });

            // Setup secondary (confirmation)
            _statusVM.SecondaryButtonText = "   Start export   ";

            // Await confirmation
            if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Secondary))
            {
                Debug.WriteLine("[Backup] User did not confirm. Aborting.");
                return;
            }

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            _statusVM.ResetStatusOverlay();
            _statusVM.StatusLabel1 = "Please wait - backing up up your collection ... ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Prepare cancellation token before starting
            var token = _statusVM.GetNewCancellationToken("Cancelling…");

            // Run backup
            var result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

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
            _statusVM.ClearCancellation();
            SetUiBusy(false);
        }

        // Use case: Check for database updates
        [RelayCommand]
        private async Task CheckForDbUpdates()
        {
            _statusVM.CancelPendingPrompt();
            var token = _statusVM.GetNewCancellationToken("Cancelling…");

            // UI state preparation
            SetUiBusy(true);
            _statusVM.ResetStatusOverlay();
            _statusVM.PrimaryButtonText = "   Cancel   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
            _statusVM.ShowStatusOverlay("One moment - checking for updates...", false);

            // Run check
            var result = await _cardDbManagementService.CheckForDbUpdatesAsync(token);

            // Show result
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

            // Reset UI state
            _statusVM.PrimaryButtonText = "   OK   ";
            _statusVM.ClearCancellation();
            SetUiBusy(false);
        }

        // Use case: Update database
        [RelayCommand]
        protected virtual async Task UpdateDB()
        {
            _statusVM.CancelPendingPrompt();
            var skipBackup = _getMyCollectionCount() == 0;
            string backupResultMessage = string.Empty;

            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Ready to update card database?", false);

            if (!skipBackup)
            {
                _statusVM.StatusLabel3 = "(we will make a backup of your collection first)";
            }

            _statusVM.PrimaryButtonText = "   Go for it!   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Wire up prompt confirmation

            if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Primary))
            {
                Debug.WriteLine("[UpdateDB] User did not confirm. Skipping update.");
                return;
            }

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            _statusVM.PrimaryButtonText = "   Cancel   ";
            var token = _statusVM.GetNewCancellationToken("Cancelling…");

            if (!skipBackup)
            {
                _statusVM.ShowStatusOverlay("Please wait - backing up up your collection ... ", false);
                var backupResult = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                if (backupResult.Code is OperationResultCode.CancelledByUser or not OperationResultCode.Success)
                {
                    _statusVM.StatusLabel1 = backupResult.Code == OperationResultCode.CancelledByUser
                        ? "Backup cancelled - aborting update..."
                        : "Backup failed - aborting update...";

                    _statusVM.StatusLabel3 = backupResult.Message;
                    _statusVM.PrimaryButtonVisibility = Visibility.Visible;
                    _statusVM.PrimaryButtonText = "  OK  ";
                    _statusVM.ClearCancellation();
                    return;
                }

                backupResultMessage = backupResult.Message;
            }
            if (token.IsCancellationRequested)
            {
                _statusVM.ClearCancellation();
                return;
            }

            _statusVM.ShowStatusOverlay("Updating database, please wait...", true);

            // Run the update
            var result = await _cardDbManagementService.UpdateDbPrepOrchetrator(ct: token);

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

            // Reset UI state before exiting
            SetUiBusy(false);
            _statusVM.ClearCancellation();
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
        }

        // Use case: Update prices
        [RelayCommand]
        protected virtual async Task UpdatePrices()
        {
            _statusVM.CancelPendingPrompt();
            _statusVM.ResetStatusOverlay();
            _statusVM.ShowStatusOverlay("Ready to update card prices?", false);

            _statusVM.PrimaryButtonText = "   Go for it!   ";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Primary))
            {
                Debug.WriteLine("[UpdatePrices] User bailed.");
                return;
            }

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            //UpdateDbVisibility = Visibility.Collapsed;
            _statusVM.PrimaryButtonText = "   Cancel   ";
            var token = _statusVM.GetNewCancellationToken("Cancelling…");

            if (token.IsCancellationRequested)
            {
                _statusVM.ClearCancellation();
                return;
            }

            _statusVM.ShowStatusOverlay("Updating card prices, please wait...", true);

            // Run the update
            var result = await _cardDbManagementService.UpdateCardPricesOrchetrator(ct: token);

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

            // Reset UI state before exiting
            _statusVM.ClearCancellation();
            SetUiBusy(false);
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;


        }

        // Cancel any active command (e.g. when navigating away)
        public void CancelActiveCommand()
        {
            _statusVM.CancelPendingPrompt();
            _statusVM.CancelCurrentOperation();
            _statusVM.ClearCancellation();

            BackupCollectionCommand.NotifyCanExecuteChanged();
            CheckForDbUpdatesCommand.NotifyCanExecuteChanged();
            UpdateDBCommand.NotifyCanExecuteChanged();
            UpdatePricesCommand.NotifyCanExecuteChanged();
        }


        // Private helpers
        private void SetUiBusy(bool isBusy)
        {
            _uiState.IsTopMenuEnabled = !isBusy;
            _uiState.SideMenuVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
            _uiState.CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }

    }
}


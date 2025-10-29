using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class UtilitiesViewModel : ObservableObject
    {
        private readonly ICardDatabaseManagementService _cardDbManagementService;
        private readonly StatusViewModel _statusVM;
        private readonly ImportViewModel _importVM;
        private readonly IUserPromptService _userPromptService;
        private readonly IUiBlockable _uiState;
        private readonly IAppRefresher _appRefresher;
        private readonly Func<int> _getMyCollectionCount;
        private readonly IFileSystemPicker _fileSystemPicker;

        // Visibility property
        [ObservableProperty]
        private Visibility updateDbVisibility = Visibility.Collapsed;

        public UtilitiesViewModel(ICardDatabaseManagementService cardDbService, StatusViewModel statusVM, ImportViewModel importVM, IUserPromptService userPromptService, ParentViewModelContext context, Func<int> collectionCountProvider, IFileSystemPicker fileSystemPicker)
        {
            _cardDbManagementService = cardDbService;
            _statusVM = statusVM;
            _importVM = importVM;
            _userPromptService = userPromptService;
            _uiState = context.UiState;
            _appRefresher = context.AppRefresher;
            _getMyCollectionCount = collectionCountProvider;
            _fileSystemPicker = fileSystemPicker;
            //ImportVM.UiBusyChanged += SetUiBusy;
        }

        // Use case: Backup collection
        [RelayCommand]
        protected virtual async Task BackupCollection()
        {
            PrepareUIForCommands("Export csv-format backup of My Collection");

            var result = new OperationResult(OperationResultCode.Error, string.Empty);
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            if (_getMyCollectionCount() == 0)
            {
                result = new OperationResult(OperationResultCode.Empty, "Your collection is empty - nothing to back up");
            }
            else
            {
                _statusVM.StatusLabel3 = $"Export to: {_cardDbManagementService.BackupFolderPath}";
                _statusVM.SecondaryButtonVisibility = Visibility.Visible;

                // Setup primary
                _statusVM.PrimaryButtonText = "   Change backup location   ";
                _statusVM.SetPrimaryAction(_ =>
                {
                    string? selectedPath = _fileSystemPicker.PickFolder("Select backup folder location", _cardDbManagementService.BackupFolderPath);
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        _cardDbManagementService.ChangeBackupFolderPath(selectedPath);
                        _statusVM.StatusLabel3 = $"Backup location: {selectedPath}";
                    }
                });

                // Await confirmation
                if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Secondary, "   Start backup   "))
                {
                    Debug.WriteLine("[Backup] User did not confirm. Aborting.");
                    return;
                }

                // UI state preparation AFTER user clicked
                SetUiBusy(true);
                _statusVM.ResetStatusOverlay();
                _statusVM.StatusLabel1 = "Please wait - backing up up your collection ... ";

                // Prepare cancellation token before starting
                var token = _statusVM.PrepareCancelButton(PromptButton.Primary);

                // Run backup
                result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                // Reset UI state
                CompleteCommandUIFlow();
            }
            // Display result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.StatusLabel1 = "Backup complete!";
                    _statusVM.StatusLabel3 = $"Backup created successfully at {result.Message}";
                    _statusVM.PrimaryButtonText = "   Awesome!   ";
                    break;

                case OperationResultCode.Empty:
                    _statusVM.StatusLabel3 = result.Message;
                    _statusVM.PrimaryButtonText = "   Oh ... I guess that makes sense...   ";
                    break;

                default:
                    _statusVM.StatusLabel3 = $"Error: {result.Message}";
                    _statusVM.PrimaryButtonText = "   Ok :-/   ";
                    break;
            }
        }

        [RelayCommand]
        protected virtual async Task ImportFromCsv()
        {
            _userPromptService.CancelPendingPrompt();
            _userPromptService.ClearCancellation();

            _statusVM.HideStatusOverlay();
            _importVM.ImportOverlayVisibility = Visibility.Visible;
            await _importVM.Begin(); // <-- activate first step
        }

        // Use case: Update prices
        [RelayCommand]
        protected virtual async Task UpdatePrices()
        {
            PrepareUIForCommands("Download and update card prices?");

            if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Primary, "   Go for it!   "))
            {
                Debug.WriteLine("[UpdatePrices] User bailed.");
                return;
            }

            // UI state preparation AFTER user clicked
            _statusVM.ResetStatusOverlay();
            SetUiBusy(true);
            _statusVM.ShowStatusOverlay("Updating card prices, please wait...", true);
            var token = _statusVM.PrepareCancelButton(PromptButton.Primary);

            // Run the update
            var result = await _cardDbManagementService.UpdateCardPricesOrchetrator(ct: token);

            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.StatusLabel1 = "Prices updated successfully!";
                    _appRefresher.RefreshAllPrices();
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.StatusLabel1 = "Update canceled";
                    _statusVM.StatusLabel3 = "Download aborted. No prices were updated.";
                    break;

                default:
                    _statusVM.StatusLabel1 = "Prices update failed!";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }
        }

        // Use case: Check for database updates
        [RelayCommand]
        private async Task CheckForDbUpdates()
        {
            PrepareUIForCommands("One moment - checking for updates...");
            SetUiBusy(true);
            var token = _statusVM.PrepareCancelButton(PromptButton.Primary);

            // Run check
            var result = await _cardDbManagementService.CheckForDbUpdatesAsync(token);

            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.UpToDate:
                    _statusVM.StatusLabel1 = "Check complete - card database is up to date.";
                    _statusVM.StatusLabel3 = result.Message;
                    break;

                case OperationResultCode.NeedsUpdate:
                    _statusVM.StatusLabel1 = "Check complete - update available.";

                    UpdateDbVisibility = Visibility.Visible;
                    _statusVM.StatusLabel3 = result.Message;

                    _statusVM.SecondaryButtonVisibility = Visibility.Visible;
                    _statusVM.SecondaryButtonText = "   Update database now   ";
                    _statusVM.SetSecondaryAction(async _ =>
                    {
                        await UpdateDB();
                    });
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.StatusLabel1 = "Cancelled";
                    _statusVM.StatusLabel3 = "No check was performed.";
                    break;

                default:
                    _statusVM.StatusLabel1 = "Error in update check.";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }
        }

        // Use case: Update database
        [RelayCommand]
        protected virtual async Task UpdateDB()
        {
            PrepareUIForCommands("Ready to update card database?");
            bool includeBackup = _getMyCollectionCount() > 0;
            string backupResultMessage = string.Empty;

            if (includeBackup)
            {
                _statusVM.StatusLabel3 = $"A csv-backup of your collection will also be created at {_cardDbManagementService.BackupFolderPath}";
            }

            if (!await _statusVM.WaitForUserConfirmationAsync(PromptButton.Primary, "   Start card database update!   "))
            {
                Debug.WriteLine("[UpdateDB] User did not confirm. Skipping update.");
                return;
            }

            // UI state preparation AFTER user clicked
            _statusVM.ResetStatusOverlay();
            SetUiBusy(true);
            var token = _statusVM.PrepareCancelButton(PromptButton.Primary);

            if (includeBackup)
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
                    _userPromptService.ClearCancellation();
                    return;
                }

                backupResultMessage = backupResult.Message;
            }
            if (token.IsCancellationRequested)
            {
                _statusVM.ResetStatusOverlay();
                _statusVM.StatusLabel1 = "Update canceled during backup stage";
                _userPromptService.ClearCancellation();
                return;
            }

            _statusVM.ShowStatusOverlay("Updating database, please wait...", true);
            token = _statusVM.PrepareCancelButton(PromptButton.Primary); // draw new token after backup

            // Run the update
            var result = await _cardDbManagementService.UpdateDbPrepOrchetrator(ct: token);

            // Reset UI state before exiting
            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.StatusLabel1 = "Database updated successfully!";
                    if (includeBackup) { _statusVM.StatusLabel3 = $"Your collection was backed up at {backupResultMessage}"; }
                    UpdateDbVisibility = Visibility.Collapsed;

                    _statusVM.StatusLabel2 = "Reloading card lists…";
                    await _appRefresher.ReloadAllCardListsAndFiltersAsync();
                    _statusVM.StatusLabel2 = string.Empty;
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.StatusLabel1 = "Update canceled";
                    _statusVM.StatusLabel3 = "Download aborted. No files were imported.";
                    break;

                default:
                    _statusVM.StatusLabel1 = "Card database update failed!";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }
        }

        // Private helpers
        private void PrepareUIForCommands(string message)
        {
            _importVM.ImportOverlayVisibility = Visibility.Collapsed;

            _userPromptService.CancelPendingPrompt();
            _userPromptService.ClearCancellation();
            _statusVM.ShowStatusOverlay(message, false);
        }
        private void CompleteCommandUIFlow()
        {
            _statusVM.ResetStatusOverlay();
            SetUiBusy(false);
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
        }
        public void SetUiBusy(bool isBusy)
        {
            _uiState.IsTopMenuEnabled = !isBusy;
            _uiState.SideMenuVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
            _uiState.CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }

    }
}


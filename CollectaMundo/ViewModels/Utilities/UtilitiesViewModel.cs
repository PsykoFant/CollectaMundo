using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class UtilitiesViewModel(IShellUiState shellUiState, ICardDatabaseManagementService cardDbService, IOperationOverlayController operationOverlayController, IUtilitiesNavigator utilitiesNavigator, IUserPromptService userPromptService, ICardCollectionHost cardCollectionHost, Func<int> collectionCountProvider, IFileSystemPicker fileSystemPicker) : ObservableObject
    {
        private readonly IShellUiState _shellUiState = shellUiState;
        private readonly ICardDatabaseManagementService _cardDbManagementService = cardDbService;
        private readonly IOperationOverlayController _operationOverlayController = operationOverlayController;
        private readonly IUtilitiesNavigator _utilitiesNavigator = utilitiesNavigator;
        private readonly IUserPromptService _userPromptService = userPromptService;
        private readonly ICardCollectionHost _cardCollectionHost = cardCollectionHost;
        private readonly Func<int> _getMyCollectionCount = collectionCountProvider;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;

        // Visibility property
        [ObservableProperty]
        private bool isUpdateDbButtonVisible;

        // Use case: Backup collection
        [RelayCommand]
        protected virtual async Task BackupCollection()
        {
            PrepareUIForActionCommands("Export csv-format backup of My Collection");

            var result = new OperationResult(OperationResultCode.Error, string.Empty);

            if (_getMyCollectionCount() == 0)
            {
                result = new OperationResult(OperationResultCode.Empty, "Your collection is empty - nothing to back up");
            }
            else
            {
                _operationOverlayController.SetDetail($"Export to: {_cardDbManagementService.BackupFolderPath}");

                // Setup primary
                _operationOverlayController.ShowPrimaryButton(
                    "   Change backup location   ",
                    _ =>
                    {
                        string? selectedPath = _fileSystemPicker.PickFolder("Select backup folder location", _cardDbManagementService.BackupFolderPath);
                        if (!string.IsNullOrWhiteSpace(selectedPath))
                        {
                            _cardDbManagementService.ChangeBackupFolderPath(selectedPath);
                            _operationOverlayController.SetDetail($"Backup location: {selectedPath}");
                        }
                    });

                // Await confirmation
                if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Secondary, "   Start backup   "))
                {
                    Debug.WriteLine("[Backup] User did not confirm. Aborting.");
                    return;
                }

                // UI state preparation AFTER user clicked
                _shellUiState.SetUiBusy(true);
                _operationOverlayController.Reset();
                _operationOverlayController.SetHeadline("Please wait - backing up up your collection ... ");

                // Prepare cancellation token before starting
                var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

                // Run backup
                result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                // Reset UI state
                CompleteActionCommandUIFlow();
            }
            // Display result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _operationOverlayController.SetHeadline("Backup complete!");
                    _operationOverlayController.SetDetail($"Backup created successfully at {result.Message}");
                    _operationOverlayController.SetPrimaryButtonText("   Awesome!   ");
                    break;

                case OperationResultCode.Empty:
                    _operationOverlayController.SetDetail(result.Message);
                    _operationOverlayController.SetPrimaryButtonText("   Oh ... I guess that makes sense...   ");
                    break;

                default:
                    _operationOverlayController.SetDetail($"Error: {result.Message}");
                    _operationOverlayController.SetPrimaryButtonText("   Ok :-/   ");
                    break;
            }
        }

        [RelayCommand]        
        protected virtual async Task ImportFromCsv()
        {
            _userPromptService.ResetInteractionState();
            _operationOverlayController.Hide();

            await _utilitiesNavigator.ShowImport(); // <-- activate first step
        }

        // Use case: Update prices
        [RelayCommand]
        protected virtual async Task UpdatePrices()
        {
            PrepareUIForActionCommands("Download and update card prices?");

            if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Primary, "   Go for it!   "))
            {
                Debug.WriteLine("[UpdatePrices] User bailed.");
                return;
            }

            // UI state preparation AFTER user clicked
            _operationOverlayController.Reset();
            _shellUiState.SetUiBusy(true);
            _operationOverlayController.Show("Updating card prices, please wait...", true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            // Run the update
            var result = await _cardDbManagementService.UpdateCardPricesOrchetrator(ct: token);

            // Clear UI
            _operationOverlayController.Reset();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:

                    // Reload collection
                    _operationOverlayController.SetStep("Refreshing prices...");
                    _cardCollectionHost.RefreshAllPrices();
                    _operationOverlayController.SetStep(string.Empty);

                    _operationOverlayController.SetHeadline("Prices updated successfully!");
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.SetHeadline("Update canceled");
                    _operationOverlayController.SetDetail("Download aborted. No prices were updated.");
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;

                default:
                    _operationOverlayController.SetHeadline("Prices update failed!");
                    _operationOverlayController.SetDetail(result.Message);
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;
            }

            _shellUiState.SetUiBusy(false);
        }

        // Use case: Check for database updates
        [RelayCommand]
        private async Task CheckForDbUpdates()
        {
            PrepareUIForActionCommands("One moment - checking for updates...");
            _shellUiState.SetUiBusy(true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            // Run check
            var result = await _cardDbManagementService.CheckForDbUpdatesAsync(token);

            CompleteActionCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.UpToDate:
                    _operationOverlayController.SetHeadline("Check complete - card database is up to date.");
                    _operationOverlayController.SetDetail(result.Message);
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;

                case OperationResultCode.NeedsUpdate:
                    _operationOverlayController.SetHeadline("Check complete - update available.");

                    IsUpdateDbButtonVisible = true;
                    _operationOverlayController.SetDetail(result.Message);

                    _operationOverlayController.ShowSecondaryButton("   Update database now   ",
                        async _ =>
                        {
                            await UpdateDB();
                        });
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.SetHeadline("Cancelled");
                    _operationOverlayController.SetDetail("No check was performed.");
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;

                default:
                    _operationOverlayController.SetHeadline("Error in update check.");
                    _operationOverlayController.SetDetail(result.Message);
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;
            }
        }

        // Use case: Update database
        [RelayCommand]
        protected virtual async Task UpdateDB()
        {
            PrepareUIForActionCommands("Ready to update card database?");
            bool includeBackup = _getMyCollectionCount() > 0;
            string backupResultMessage = string.Empty;

            if (includeBackup)
            {
                _operationOverlayController.SetDetail($"A csv-backup of your collection will also be created at {_cardDbManagementService.BackupFolderPath}");
            }

            if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Primary, "   Start card database update!   "))
            {
                Debug.WriteLine("[UpdateDB] User did not confirm. Skipping update.");
                return;
            }

            // UI state preparation AFTER user clicked
            _operationOverlayController.Reset();
            _shellUiState.SetUiBusy(true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            if (includeBackup)
            {
                _operationOverlayController.Show("Please wait - backing up up your collection ... ", false);
                var backupResult = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                if (backupResult.Code is OperationResultCode.CancelledByUser or not OperationResultCode.Success)
                {
                    _operationOverlayController.SetHeadline(
                        backupResult.Code == OperationResultCode.CancelledByUser
                        ? "Backup cancelled - aborting update..."
                        : "Backup failed - aborting update...");

                    _operationOverlayController.SetDetail(backupResult.Message);
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    _userPromptService.DisposeOperationCancellationToken();
                    return;
                }

                backupResultMessage = backupResult.Message;
            }
            if (token.IsCancellationRequested)
            {
                _operationOverlayController.Reset();
                _operationOverlayController.SetHeadline("Update canceled during backup stage");
                _userPromptService.DisposeOperationCancellationToken();
                return;
            }

            _operationOverlayController.Show("Updating database, please wait...", true);
            token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary); // draw new token after backup

            // Run the update
            var result = await _cardDbManagementService.UpdateDbPrepOrchetrator(ct: token);

            // Clear UI
            _operationOverlayController.Reset();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:

                    // Reload collection
                    _operationOverlayController.SetStep("Reloading card lists…");
                    await _cardCollectionHost.ReloadAllCardListsAndFiltersAsync();
                    _operationOverlayController.SetStep(string.Empty);

                    // Show success message                    
                    _operationOverlayController.SetHeadline("Database updated successfully!");
                    if (includeBackup) { _operationOverlayController.SetDetail($"Your collection was backed up at {backupResultMessage}"); }
                    IsUpdateDbButtonVisible = false;
                    _operationOverlayController.SetPrimaryButtonText("   I love an updated database!   ");
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.SetHeadline("Update canceled");
                    _operationOverlayController.SetDetail("Download aborted. No files were imported.");
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;

                default:
                    _operationOverlayController.SetHeadline("Card database update failed!");
                    _operationOverlayController.SetDetail(result.Message);
                    _operationOverlayController.SetPrimaryButtonText("  OK  ");
                    break;
            }

            _shellUiState.SetUiBusy(false);
        }

        // Use case: Manage card locations
        [RelayCommand]
        protected virtual async Task ManageCardLocations()
        {
            _userPromptService.ResetInteractionState();
            _operationOverlayController.Hide();

            await _utilitiesNavigator.ShowCardLocationManagement();
        }

        // Private helpers
        private void PrepareUIForActionCommands(string message)
        {
            _utilitiesNavigator.ShowHome();
            _userPromptService.ResetInteractionState();
            _operationOverlayController.Show(message, false);
        }
        private void CompleteActionCommandUIFlow()
        {
            _operationOverlayController.Reset();
            _shellUiState.SetUiBusy(false);
        }
    }
}


using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class UtilitiesViewModel(ICardDatabaseManagementService cardDbService, IOperationOverlayController operationOverlayController, ImportViewModel importVM, IUserPromptService userPromptService, ICardCollectionHost cardCollectionHost, Func<int> collectionCountProvider, IFileSystemPicker fileSystemPicker) : ObservableObject
    {
        private readonly ICardDatabaseManagementService _cardDbManagementService = cardDbService;
        private readonly IOperationOverlayController _operationOverlayController = operationOverlayController;
        private readonly ImportViewModel _importVM = importVM;
        private readonly IUserPromptService _userPromptService = userPromptService;
        private readonly ICardCollectionHost _cardCollectionHost = cardCollectionHost;
        private readonly Func<int> _getMyCollectionCount = collectionCountProvider;
        private readonly IFileSystemPicker _fileSystemPicker = fileSystemPicker;

        // Visibility property
        [ObservableProperty]
        private Visibility updateDbVisibility = Visibility.Collapsed;

        // Use case: Backup collection
        [RelayCommand]
        protected virtual async Task BackupCollection()
        {
            PrepareUIForCommands("Export csv-format backup of My Collection");

            var result = new OperationResult(OperationResultCode.Error, string.Empty);
            _operationOverlayController.PrimaryButtonVisibility = Visibility.Visible;

            if (_getMyCollectionCount() == 0)
            {
                result = new OperationResult(OperationResultCode.Empty, "Your collection is empty - nothing to back up");
            }
            else
            {
                _operationOverlayController.StatusLabel3 = $"Export to: {_cardDbManagementService.BackupFolderPath}";
                _operationOverlayController.SecondaryButtonVisibility = Visibility.Visible;

                // Setup primary
                _operationOverlayController.PrimaryButtonText = "   Change backup location   ";
                _operationOverlayController.SetPrimaryAction(_ =>
                {
                    string? selectedPath = _fileSystemPicker.PickFolder("Select backup folder location", _cardDbManagementService.BackupFolderPath);
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        _cardDbManagementService.ChangeBackupFolderPath(selectedPath);
                        _operationOverlayController.StatusLabel3 = $"Backup location: {selectedPath}";
                    }
                });

                // Await confirmation
                if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Secondary, "   Start backup   "))
                {
                    Debug.WriteLine("[Backup] User did not confirm. Aborting.");
                    return;
                }

                // UI state preparation AFTER user clicked
                _cardCollectionHost.SetUiBusy(true);
                _operationOverlayController.ResetStatusOverlay();
                _operationOverlayController.StatusLabel1 = "Please wait - backing up up your collection ... ";

                // Prepare cancellation token before starting
                var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

                // Run backup
                result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                // Reset UI state
                CompleteCommandUIFlow();
            }
            // Display result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _operationOverlayController.StatusLabel1 = "Backup complete!";
                    _operationOverlayController.StatusLabel3 = $"Backup created successfully at {result.Message}";
                    _operationOverlayController.PrimaryButtonText = "   Awesome!   ";
                    break;

                case OperationResultCode.Empty:
                    _operationOverlayController.StatusLabel3 = result.Message;
                    _operationOverlayController.PrimaryButtonText = "   Oh ... I guess that makes sense...   ";
                    break;

                default:
                    _operationOverlayController.StatusLabel3 = $"Error: {result.Message}";
                    _operationOverlayController.PrimaryButtonText = "   Ok :-/   ";
                    break;
            }
        }

        [RelayCommand]
        protected virtual async Task ImportFromCsv()
        {
            _userPromptService.CancelPendingPrompt();
            _userPromptService.ClearCancellation();

            _operationOverlayController.HideStatusOverlay();
            _importVM.ImportOverlayVisibility = Visibility.Visible;
            await _importVM.Begin(); // <-- activate first step
        }

        // Use case: Update prices
        [RelayCommand]
        protected virtual async Task UpdatePrices()
        {
            PrepareUIForCommands("Download and update card prices?");

            if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Primary, "   Go for it!   "))
            {
                Debug.WriteLine("[UpdatePrices] User bailed.");
                return;
            }

            // UI state preparation AFTER user clicked
            _operationOverlayController.ResetStatusOverlay();
            _cardCollectionHost.SetUiBusy(true);
            _operationOverlayController.ShowStatusOverlay("Updating card prices, please wait...", true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            // Run the update
            var result = await _cardDbManagementService.UpdateCardPricesOrchetrator(ct: token);

            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _operationOverlayController.StatusLabel1 = "Prices updated successfully!";
                    _cardCollectionHost.RefreshAllPrices();
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.StatusLabel1 = "Update canceled";
                    _operationOverlayController.StatusLabel3 = "Download aborted. No prices were updated.";
                    break;

                default:
                    _operationOverlayController.StatusLabel1 = "Prices update failed!";
                    _operationOverlayController.StatusLabel3 = result.Message;
                    break;
            }
        }

        // Use case: Check for database updates
        [RelayCommand]
        private async Task CheckForDbUpdates()
        {
            PrepareUIForCommands("One moment - checking for updates...");
            _cardCollectionHost.SetUiBusy(true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            // Run check
            var result = await _cardDbManagementService.CheckForDbUpdatesAsync(token);

            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.UpToDate:
                    _operationOverlayController.StatusLabel1 = "Check complete - card database is up to date.";
                    _operationOverlayController.StatusLabel3 = result.Message;
                    break;

                case OperationResultCode.NeedsUpdate:
                    _operationOverlayController.StatusLabel1 = "Check complete - update available.";

                    UpdateDbVisibility = Visibility.Visible;
                    _operationOverlayController.StatusLabel3 = result.Message;

                    _operationOverlayController.SecondaryButtonVisibility = Visibility.Visible;
                    _operationOverlayController.SecondaryButtonText = "   Update database now   ";
                    _operationOverlayController.SetSecondaryAction(async _ =>
                    {
                        await UpdateDB();
                    });
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.StatusLabel1 = "Cancelled";
                    _operationOverlayController.StatusLabel3 = "No check was performed.";
                    break;

                default:
                    _operationOverlayController.StatusLabel1 = "Error in update check.";
                    _operationOverlayController.StatusLabel3 = result.Message;
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
                _operationOverlayController.StatusLabel3 = $"A csv-backup of your collection will also be created at {_cardDbManagementService.BackupFolderPath}";
            }

            if (!await _operationOverlayController.WaitForUserConfirmationAsync(PromptButton.Primary, "   Start card database update!   "))
            {
                Debug.WriteLine("[UpdateDB] User did not confirm. Skipping update.");
                return;
            }

            // UI state preparation AFTER user clicked
            _operationOverlayController.ResetStatusOverlay();
            _cardCollectionHost.SetUiBusy(true);
            var token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary);

            if (includeBackup)
            {
                _operationOverlayController.ShowStatusOverlay("Please wait - backing up up your collection ... ", false);
                var backupResult = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(token));

                if (backupResult.Code is OperationResultCode.CancelledByUser or not OperationResultCode.Success)
                {
                    _operationOverlayController.StatusLabel1 = backupResult.Code == OperationResultCode.CancelledByUser
                        ? "Backup cancelled - aborting update..."
                        : "Backup failed - aborting update...";

                    _operationOverlayController.StatusLabel3 = backupResult.Message;
                    _operationOverlayController.PrimaryButtonVisibility = Visibility.Visible;
                    _operationOverlayController.PrimaryButtonText = "  OK  ";
                    _userPromptService.ClearCancellation();
                    return;
                }

                backupResultMessage = backupResult.Message;
            }
            if (token.IsCancellationRequested)
            {
                _operationOverlayController.ResetStatusOverlay();
                _operationOverlayController.StatusLabel1 = "Update canceled during backup stage";
                _userPromptService.ClearCancellation();
                return;
            }

            _operationOverlayController.ShowStatusOverlay("Updating database, please wait...", true);
            token = _operationOverlayController.PrepareCancelButton(PromptButton.Primary); // draw new token after backup

            // Run the update
            var result = await _cardDbManagementService.UpdateDbPrepOrchetrator(ct: token);

            // Reset UI state before exiting
            CompleteCommandUIFlow();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _operationOverlayController.StatusLabel1 = "Database updated successfully!";
                    if (includeBackup) { _operationOverlayController.StatusLabel3 = $"Your collection was backed up at {backupResultMessage}"; }
                    UpdateDbVisibility = Visibility.Collapsed;

                    _operationOverlayController.StatusLabel2 = "Reloading card lists…";
                    await _cardCollectionHost.ReloadAllCardListsAndFiltersAsync();
                    _operationOverlayController.StatusLabel2 = string.Empty;
                    break;

                case OperationResultCode.CancelledByUser:
                    _operationOverlayController.StatusLabel1 = "Update canceled";
                    _operationOverlayController.StatusLabel3 = "Download aborted. No files were imported.";
                    break;

                default:
                    _operationOverlayController.StatusLabel1 = "Card database update failed!";
                    _operationOverlayController.StatusLabel3 = result.Message;
                    break;
            }
        }

        // Private helpers
        private void PrepareUIForCommands(string message)
        {
            _importVM.ImportOverlayVisibility = Visibility.Collapsed;
            _userPromptService.CancelPendingPrompt();
            _userPromptService.ClearCancellation();
            _operationOverlayController.ShowStatusOverlay(message, false);
        }
        private void CompleteCommandUIFlow()
        {
            _operationOverlayController.ResetStatusOverlay();
            _cardCollectionHost.SetUiBusy(false);
            _operationOverlayController.PrimaryButtonVisibility = Visibility.Visible;
        }
    }
}


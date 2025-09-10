using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class UpdateViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly ICardDatabaseManagementService _cardDbManagementService;
        private readonly StatusViewModel _statusVM;
        private readonly IUiBlockable _uiState;
        private readonly IAppRefresher _appRefresher;
        private readonly Func<int> _getMyCollectionCount;

        // Cancellation tokens
        private CancellationTokenSource? _backupCts;
        private CancellationTokenSource? _checkCts;
        private CancellationTokenSource? _updateCts;

        // Commands
        public ICommand BackupCollectionCommand { get; private set; } = null!;
        public ICommand CheckForDbUpdatesCommand { get; private set; } = null!;
        public ICommand UpdateDBCommand { get; protected set; } = null!;

        // Visibility properties
        private Visibility _updateDbVisibility = Visibility.Collapsed;
        public Visibility UpdateDbVisibility
        {
            get => _updateDbVisibility;
            set { _updateDbVisibility = value; OnPropertyChanged(); }
        }

        // Constructor
        public UpdateViewModel(ICardDatabaseManagementService cardDbManagementService, StatusViewModel statusVM, IUiBlockable uiState, IAppRefresher appRefresher, Func<int> getMyCollectionCount)

        {
            _cardDbManagementService = cardDbManagementService;
            _statusVM = statusVM;
            _uiState = uiState;
            _appRefresher = appRefresher;
            _getMyCollectionCount = getMyCollectionCount;

            BackupCollectionCommand = new RelayCommand<object>(async _ => await BackupCollectionAsync());
            CheckForDbUpdatesCommand = new RelayCommand<object>(async _ => await CheckForDbUpdatesAsync());
            UpdateDBCommand = new RelayCommand<object>(async _ => await UpdateDBAsync());
        }

        // Use case: Backup collection
        private async Task BackupCollectionAsync()
        {
            // UI state preparation
            SetUiBusy(true);
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;
            string emptyMessage = "Your collection is empty - nothing to back up";
            string emptyAckText = "Oh ... I guess that makes sense...";

            // Check if collection is empty
            if (_getMyCollectionCount() == 0)
            {
                _statusVM.ShowStatusOverlay(emptyMessage, false);
                _statusVM.PrimaryButtonText = emptyAckText;
            }
            else
            {
                // Prepare cancellation
                _backupCts = new CancellationTokenSource();
                _statusVM.SetPrimaryAction(_ =>
                {
                    _statusVM.StatusLabel2 = "Cancelling…";
                    _backupCts?.Cancel();
                });

                // Run backup
                _statusVM.ShowStatusOverlay("Please wait - backing up up your collection ... ", false);
                var result = await Task.Run(() => _cardDbManagementService.ExportCollectionAsync(_backupCts.Token));

                // Revert primary button to default
                _statusVM.SetPrimaryAction(_ =>
                {
                    _statusVM.StatusLabel2 = string.Empty;
                    _statusVM.HideStatusOverlay();
                });

                // Display result
                switch (result.Code)
                {
                    case OperationResultCode.Success:
                        _statusVM.StatusLabel1 = $"Backup created successfully at {result.Message}";
                        _statusVM.PrimaryButtonText = "Awesome!";
                        break;

                    case OperationResultCode.Empty:
                        _statusVM.ShowStatusOverlay(emptyMessage, false);
                        _statusVM.PrimaryButtonText = emptyAckText;
                        break;

                    case OperationResultCode.Error:
                        _statusVM.StatusLabel1 = $"Error: {result.Message}";
                        _statusVM.PrimaryButtonText = "Ok :-/";
                        break;
                }
            }
            // Reset UI state
            _backupCts = null;
            SetUiBusy(false);
        }

        // Use case: Check for database updates
        private async Task CheckForDbUpdatesAsync()
        {
            _checkCts = new CancellationTokenSource();

            // UI state preparation
            SetUiBusy(true);
            _statusVM.PrimaryButtonText = "Cancel";
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
            _statusVM.PrimaryButtonText = "OK";
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
        private async Task UpdateDBAsync()
        {
            var skipBackup = _getMyCollectionCount() == 0;
            string backupResultMessage = string.Empty;
            _statusVM.ShowStatusOverlay("Ready to update card database?", false);
            if (!skipBackup) { _statusVM.StatusLabel3 = "(we will make a backup of your collection first)"; }

            _statusVM.PrimaryButtonText = "Go for it!";
            _statusVM.PrimaryButtonVisibility = Visibility.Visible;

            // Wait for user confirmation before proceeding
            var tcs = new TaskCompletionSource();
            _statusVM.SetPrimaryAction(_ => tcs.SetResult());
            await tcs.Task;

            // UI state preparation AFTER user clicked
            SetUiBusy(true);
            UpdateDbVisibility = Visibility.Collapsed;
            _statusVM.PrimaryButtonText = "Cancel";
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
                if (backupResult.Code != OperationResultCode.Success)
                {
                    _statusVM.StatusLabel1 = "Backup failed - aborting update...";
                    _statusVM.PrimaryButtonVisibility = Visibility.Visible;
                    _statusVM.PrimaryButtonText = "Ok";
                    _statusVM.SetPrimaryAction(_ =>
                    {
                        _statusVM.StatusLabel2 = string.Empty;
                        _statusVM.HideStatusOverlay();
                    });
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

            // Reset UI state before showing result
            _statusVM.ResetStatusOverlay();

            // Show result
            switch (result.Code)
            {
                case OperationResultCode.Success:
                    _statusVM.StatusLabel3 = "Reloading card lists…";
                    await _appRefresher.ReloadAllCardListsAndFiltersAsync();
                    _statusVM.StatusLabel1 = "Database updated successfully!";
                    if (!skipBackup) { _statusVM.StatusLabel3 = $"Your collection was backed up at {backupResultMessage}!"; }
                    _statusVM.PrimaryButtonVisibility = Visibility.Visible;
                    break;

                case OperationResultCode.CancelledByUser:
                    _statusVM.StatusLabel1 = "Update canceled";
                    _statusVM.StatusLabel3 = "Download aborted. No files were imported.";
                    break;

                default:
                    _statusVM.StatusLabel1 = "Update failed!";
                    _statusVM.StatusLabel3 = result.Message;
                    break;
            }

            _updateCts = null;
            SetUiBusy(false);
            _statusVM.SetPrimaryAction(null); // revert to default action (hide overlay)
        }


        private void SetUiBusy(bool isBusy)
        {
            _uiState.IsTopMenuEnabled = !isBusy;
            _uiState.SideMenuVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
            _uiState.CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

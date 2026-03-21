using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shell;
using Moq;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUtilitiesViewModel(
    IShellUiState shellUiState,
    ICardDatabaseManagementService dbService,
    IOperationOverlayController operationOverlayController,
    IImportOverlayController importOverlayController,
    IUserPromptService userPromptService,
    ICardCollectionHost parentCtx,
    Func<int> getMyCollectionCount,
    IFileSystemPicker fileSystemPicker)
    : UtilitiesViewModel(
        shellUiState,
        dbService,
        operationOverlayController,
        importOverlayController,
        userPromptService,
        parentCtx,
        getMyCollectionCount,
        fileSystemPicker)
    {
        public Task InternalUpdateTask => _internalUpdateTask!;
        private Task? _internalUpdateTask;

        protected override Task UpdateDB()
        {
            _internalUpdateTask = base.UpdateDB();
            return _internalUpdateTask;
        }

        protected override Task BackupCollection()
        {
            _internalUpdateTask = base.BackupCollection();
            return _internalUpdateTask;
        }
    }


    public sealed class FakeOperationOverlayController : IOperationOverlayController
    {
        private TaskCompletionSource<bool>? _confirmationTcs;
        private CancellationTokenSource _cts = new();

        public string Headline { get; private set; } = string.Empty;
        public string Detail { get; private set; } = string.Empty;
        public string Step { get; private set; } = string.Empty;

        public int ProgressValue { get; private set; }
        public bool IsLogoVisible { get; private set; }
        public bool IsProgressVisible { get; private set; }
        public bool IsSetupFailVisible { get; private set; }

        public string PrimaryButtonText { get; private set; } = string.Empty;
        public string SecondaryButtonText { get; private set; } = string.Empty;

        public Visibility PrimaryButtonVisibility { get; private set; } = Visibility.Collapsed;
        public Visibility SecondaryButtonVisibility { get; private set; } = Visibility.Collapsed;

        public bool IsVisible { get; private set; }

        public Action<object?>? PrimaryButtonAction { get; private set; }
        public Action<object?>? SecondaryButtonAction { get; private set; }

        public void Show(string headline, bool showProgress = false)
        {
            IsVisible = true;
            Headline = headline;
            IsProgressVisible = showProgress;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void Reset()
        {
            Headline = string.Empty;
            Detail = string.Empty;
            Step = string.Empty;
            ProgressValue = 0;
            IsLogoVisible = false;
            IsProgressVisible = false;
            IsSetupFailVisible = false;

            PrimaryButtonText = string.Empty;
            SecondaryButtonText = string.Empty;
            PrimaryButtonVisibility = Visibility.Collapsed;
            SecondaryButtonVisibility = Visibility.Collapsed;
            PrimaryButtonAction = null;
            SecondaryButtonAction = null;
        }

        public void SetHeadline(string text) => Headline = text;
        public void SetDetail(string text) => Detail = text;
        public void SetStep(string text) => Step = text;
        public void SetProgress(int value) => ProgressValue = value;

        public void ShowLogo(bool show) => IsLogoVisible = show;
        public void ShowProgress(bool show) => IsProgressVisible = show;
        public void ShowSetupFailure(bool show) => IsSetupFailVisible = show;

        public void ShowPrimaryButton(string text, Action<object?>? action = null)
        {
            PrimaryButtonText = text;
            PrimaryButtonVisibility = Visibility.Visible;
            PrimaryButtonAction = action;
        }

        public void SetPrimaryButtonText(string text)
        {
            PrimaryButtonVisibility = Visibility.Visible;
            PrimaryButtonText = text;
        }

        public void HidePrimaryButton()
        {
            PrimaryButtonVisibility = Visibility.Collapsed;
            PrimaryButtonText = string.Empty;
            PrimaryButtonAction = null;
        }

        public void ShowSecondaryButton(string text, Action<object?>? action = null)
        {
            SecondaryButtonText = text;
            SecondaryButtonVisibility = Visibility.Visible;
            SecondaryButtonAction = action;
        }

        public void SetSecondaryButtonText(string text)
        {
            SecondaryButtonVisibility = Visibility.Visible;
            SecondaryButtonText = text;
        }

        public void HideSecondaryButton()
        {
            SecondaryButtonVisibility = Visibility.Collapsed;
            SecondaryButtonText = string.Empty;
            SecondaryButtonAction = null;
        }

        public CancellationToken PrepareCancelButton(PromptButton button)
        {
            _cts = new CancellationTokenSource();

            if (button == PromptButton.Primary)
            {
                ShowPrimaryButton("Cancel", _ => _cts.Cancel());
            }
            else
            {
                ShowSecondaryButton("Cancel", _ => _cts.Cancel());
            }

            return _cts.Token;
        }

        public Task<bool> WaitForUserConfirmationAsync(PromptButton button, string confirmText)
        {
            _confirmationTcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (button == PromptButton.Primary)
            {
                ShowPrimaryButton(confirmText, _ => _confirmationTcs.TrySetResult(true));
            }
            else
            {
                ShowSecondaryButton(confirmText, _ => _confirmationTcs.TrySetResult(true));
            }

            return _confirmationTcs.Task;
        }

        public void Confirm() => _confirmationTcs?.TrySetResult(true);
        public void Decline() => _confirmationTcs?.TrySetResult(false);
    }






    public static class StatusTestDriver
    {
        public static async Task WaitUntilPrimaryButtonTextAsync(
            FakeOperationOverlayController overlay,
            string expectedText,
            TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();

            while (overlay.PrimaryButtonText != expectedText)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for PrimaryButtonText == \"{expectedText}\"");
                }

                await Task.Delay(10);
            }
        }

        public static async Task WaitUntilSecondaryButtonTextAsync(
            FakeOperationOverlayController overlay,
            string expectedText,
            TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();

            while (overlay.SecondaryButtonText != expectedText)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for SecondaryButtonText == \"{expectedText}\"");
                }

                await Task.Delay(10);
            }
        }

        public static void ClickPrimaryButton(FakeOperationOverlayController overlay)
        {
            if (overlay.PrimaryButtonAction is null)
            {
                throw new InvalidOperationException("PrimaryButtonAction is not assigned.");
            }

            overlay.PrimaryButtonAction(null);
        }

        public static void ClickSecondaryButton(FakeOperationOverlayController overlay)
        {
            if (overlay.SecondaryButtonAction is null)
            {
                throw new InvalidOperationException("SecondaryButtonAction is not assigned.");
            }

            overlay.SecondaryButtonAction(null);
        }

        public static void Confirm(FakeOperationOverlayController overlay) => overlay.Confirm();
        public static void Decline(FakeOperationOverlayController overlay) => overlay.Decline();
    }





    public class UpdateTestContext
    {
        public TestableUtilitiesViewModel UtilitiesVM { get; set; } = null!;
        public FakeOperationOverlayController Overlay { get; set; } = null!;
        public IUserPromptService UserPromptService { get; set; } = null!;
        public Mock<ICardDatabaseManagementService> DbServiceMock { get; set; } = null!;
        public Mock<ICardCollectionHost> CardCollectionHostMock { get; set; } = null!;
        public Mock<IShellUiState> ShellUiStateMock { get; set; } = null!;
    }

    public class UpdateTestContextBuilder
    {
        private OperationResult? _backupResult;
        private OperationResult? _updateResult;
        private bool _skipBackupWithError;
        private Func<int>? _collectionCount;
        private Func<CancellationToken, Task<OperationResult>>? _customUpdateOrchestrator;

        public UpdateTestContextBuilder WithBackupResult(OperationResult result)
        {
            _backupResult = result;
            return this;
        }

        public UpdateTestContextBuilder WithUpdateResult(OperationResult result)
        {
            _updateResult = result;
            return this;
        }

        public UpdateTestContextBuilder WithCollectionCount(int count)
        {
            _collectionCount = () => count;
            return this;
        }

        public UpdateTestContextBuilder WithCustomUpdateOrchestrator(Func<CancellationToken, Task<OperationResult>> orchestrator)
        {
            _customUpdateOrchestrator = orchestrator;
            return this;
        }

        public UpdateTestContextBuilder WithSkippedBackup()
        {
            _skipBackupWithError = true;
            return this;
        }

        public UpdateTestContext Build()
        {
            var dbService = new Mock<ICardDatabaseManagementService>();
            dbService.SetupGet(s => s.BackupFolderPath).Returns("mock-backup-folder");

            if (_skipBackupWithError)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()))
                    .Throws(new InvalidOperationException("Backup should not be called"));
            }
            else if (_backupResult != null)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_backupResult);
            }

            if (_customUpdateOrchestrator != null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns((int _, CancellationToken ct) => _customUpdateOrchestrator(ct));
            }
            else if (_updateResult != null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_updateResult);
            }

            var shellUiState = new Mock<IShellUiState>();
            var importOverlayController = new Mock<IImportOverlayController>();
            var parentCtx = new Mock<ICardCollectionHost>();
            var userPromptService = new UserPromptService();
            var overlay = new FakeOperationOverlayController();

            var utilitiesVM = new TestableUtilitiesViewModel(
                shellUiState.Object,
                dbService.Object,
                overlay,
                importOverlayController.Object,
                userPromptService,
                parentCtx.Object,
                _collectionCount ?? (() => 5),
                new Mock<IFileSystemPicker>().Object);

            return new UpdateTestContext
            {
                UtilitiesVM = utilitiesVM,
                Overlay = overlay,
                UserPromptService = userPromptService,
                DbServiceMock = dbService,
                CardCollectionHostMock = parentCtx,
                ShellUiStateMock = shellUiState
            };
        }

        public static UpdateTestContextBuilder Builder => new();
    }
}

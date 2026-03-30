using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shared;
using CollectaMundo.ViewModels.Shell;
using CollectaMundo.ViewModels.Utilities;
using Moq;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUtilitiesViewModel(
    IShellUiState shellUiState,
    ICardDatabaseManagementService dbService,
    IOperationOverlayController operationOverlayController,
    IUtilitiesNavigator utilitiesNavigator,
    IUserPromptService userPromptService,
    ICardCollectionHost parentCtx,
    Func<int> getMyCollectionCount,
    IFileSystemPicker fileSystemPicker)
    : UtilitiesViewModel(
        shellUiState,
        dbService,
        operationOverlayController,
        utilitiesNavigator,
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
    public class UpdateTestContext
    {
        public TestableUtilitiesViewModel UtilitiesVM { get; set; } = null!;
        public OperationOverlayViewModel OverlayVM { get; set; } = null!;
        public OperationOverlayController OperationOverlayController { get; set; } = null!;
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
            var utilitiesNavigator = new Mock<IUtilitiesNavigator>();
            var parentCtx = new Mock<ICardCollectionHost>();
            var userPromptService = new UserPromptService();

            var overlayVm = new OperationOverlayViewModel(userPromptService);
            var overlayController = new OperationOverlayController(overlayVm);

            var utilitiesVM = new TestableUtilitiesViewModel(
                shellUiState.Object,
                dbService.Object,
                overlayController,
                utilitiesNavigator.Object,
                userPromptService,
                parentCtx.Object,
                _collectionCount ?? (() => 5),
                new Mock<IFileSystemPicker>().Object);

            return new UpdateTestContext
            {
                UtilitiesVM = utilitiesVM,
                OverlayVM = overlayVm,
                OperationOverlayController = overlayController,
                UserPromptService = userPromptService,
                DbServiceMock = dbService,
                CardCollectionHostMock = parentCtx,
                ShellUiStateMock = shellUiState
            };
        }
        public static UpdateTestContextBuilder Builder => new();
    }

    public static class StatusTestDriver
    {
        public static async Task WaitUntilPrimaryButtonTextAsync(OperationOverlayViewModel overlayVm,string expectedText,TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();

            while (overlayVm.PrimaryButtonText != expectedText)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for PrimaryButtonText == \"{expectedText}\"");
                }

                await Task.Delay(10);
            }
        }
        public static async Task WaitUntilSecondaryButtonTextAsync(OperationOverlayViewModel overlayVm, string expectedText, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();

            while (overlayVm.SecondaryButtonText != expectedText)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for SecondaryButtonText == \"{expectedText}\"");
                }

                await Task.Delay(10);
            }
        }
        public static void ClickPrimaryButton(OperationOverlayViewModel overlayVm)
        {
            if (!overlayVm.PrimaryActionCommand.CanExecute(null))
            {
                throw new InvalidOperationException("PrimaryActionCommand is not executable.");
            }

            overlayVm.PrimaryActionCommand.Execute(null);
        }
        public static void ClickSecondaryButton(OperationOverlayViewModel overlayVm)
        {
            if (!overlayVm.SecondaryActionCommand.CanExecute(null))
            {
                throw new InvalidOperationException("SecondaryActionCommand is not executable.");
            }

            overlayVm.SecondaryActionCommand.Execute(null);
        }   
    }
}

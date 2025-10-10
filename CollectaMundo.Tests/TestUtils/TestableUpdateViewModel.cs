using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels;
using Moq;
using System.Diagnostics;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUpdateViewModel(ICardDatabaseManagementService dbService, StatusViewModel statusVM, IUiBlockable uiState, IAppRefresher appRefresher, Func<int> getMyCollectionCount) : UpdateViewModel(dbService, statusVM, uiState, appRefresher, getMyCollectionCount, "")
    {
        public Task InternalUpdateTask => _internalUpdateTask!;
        private Task? _internalUpdateTask;

        // Override and capture the task when it's invoked by RelayCommand
        protected override Task UpdateDBAsync()
        {
            _internalUpdateTask = base.UpdateDBAsync();
            return _internalUpdateTask;
        }
    }
    public static class StatusTestDriver
    {
        public static async Task WaitUntilButtonTextAsync(StatusViewModel vm, string expectedText, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();
            while (vm.PrimaryButtonText != expectedText)
            {
                if (sw.Elapsed > timeout)
                    throw new TimeoutException($"Timed out waiting for PrimaryButtonText == \"{expectedText}\"");

                await Task.Delay(10);
            }
        }

        public static void ClickPrimaryButton(StatusViewModel vm)
        {
            if (vm.PrimaryButtonCommand.CanExecute(null))
                vm.PrimaryButtonCommand.Execute(null);
            else
                throw new InvalidOperationException("PrimaryButtonCommand is not executable.");
        }
    }
    public class UpdateTestContext
    {
        public TestableUpdateViewModel UpdateVM { get; set; } = null!;
        public StatusViewModel StatusVM { get; set; } = null!;
        public Mock<ICardDatabaseManagementService> DbServiceMock { get; set; } = null!;
    }
    public class UpdateTestContextBuilder
    {
        private OperationResult? _backupResult;
        private OperationResult? _updateResult;
        private bool _skipBackupWithError;
        private Func<int>? _collectionCount;
        private Func<int, CancellationToken, Task<OperationResult>>? _customUpdateOrchestrator;

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

        public UpdateTestContextBuilder WithCustomUpdateOrchestrator(Func<int, CancellationToken, Task<OperationResult>> orchestrator)
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

            if (_backupResult != null)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(_backupResult);
            }

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
                         .Returns(_customUpdateOrchestrator);
            }
            else if (_updateResult != null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(_updateResult);
            }

            var statusVM = new StatusViewModel();
            var uiState = new Mock<IUiBlockable>();
            var appRefresher = new Mock<IAppRefresher>();

            var updateVM = new TestableUpdateViewModel(
                dbService.Object,
                statusVM,
                uiState.Object,
                appRefresher.Object,
                _collectionCount ?? (() => 5));

            return new UpdateTestContext
            {
                UpdateVM = updateVM,
                StatusVM = statusVM,
                DbServiceMock = dbService
            };
        }

        public static UpdateTestContextBuilder Builder => new();
    }
}

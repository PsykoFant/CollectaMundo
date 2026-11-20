using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardDatabaseManagement.CardDatabaseUpdateWorkflow;
using CollectaMundo.ApplicationServices.CollectionExport;
using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ViewModels.Shared;
using CollectaMundo.ViewModels.Shell;
using CollectaMundo.ViewModels.Utilities;
using Moq;
using System.Diagnostics;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUtilitiesViewModel(ICardDatabaseManagementService dbService, ICardDatabaseUpdateWorkflowService updateWorkflowService, IOperationOverlayController operationOverlayController, IUtilitiesNavigator utilitiesNavigator, IUserPromptService userPromptService, ICardCollectionHost parentCtx) :
        UtilitiesViewModel(dbService, updateWorkflowService, operationOverlayController, utilitiesNavigator, userPromptService, parentCtx)
    {
        public Task InternalUpdateTask => _internalUpdateTask!;
        private Task? _internalUpdateTask;

        protected override Task UpdateDB()
        {
            _internalUpdateTask = base.UpdateDB();
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
        public Mock<ICollectionExportService> CollectionExportServiceMock { get; set; } = null!;
        public Mock<ICardCollectionHost> CardCollectionHostMock { get; set; } = null!;
    }
    public class UpdateTestContextBuilder
    {
        private OperationResult? _backupResult;
        private OperationResult? _updateResult;
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

        public UpdateTestContextBuilder WithCustomUpdateOrchestrator(Func<CancellationToken, Task<OperationResult>> orchestrator)
        {
            _customUpdateOrchestrator = orchestrator;
            return this;
        }
        public UpdateTestContextBuilder WithEmptyCollection()
        {
            _backupResult = new OperationResult(
                OperationResultCode.Empty,
                "Collection is empty.");

            return this;
        }

        public UpdateTestContext Build()
        {
            var dbService = new Mock<ICardDatabaseManagementService>();
            var collectionExportService = new Mock<ICollectionExportService>();

            collectionExportService
                .Setup(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_backupResult ?? new OperationResult(OperationResultCode.Success, "mock-backup.csv"));

            if (_customUpdateOrchestrator is not null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns((int _, CancellationToken ct) => _customUpdateOrchestrator(ct));
            }
            else
            {
                dbService.Setup(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(_updateResult ?? new OperationResult(OperationResultCode.Success));
            }

            var updateWorkflowService = new CardDatabaseUpdateWorkflowService(collectionExportService.Object, dbService.Object);

            var utilitiesNavigator = new Mock<IUtilitiesNavigator>();
            var parentCtx = new Mock<ICardCollectionHost>();
            var userPromptService = new UserPromptService();
            var overlayVm = new OperationOverlayViewModel(userPromptService);
            var overlayController = new OperationOverlayController(overlayVm);
            var utilitiesVM = new TestableUtilitiesViewModel(dbService.Object, updateWorkflowService, overlayController, utilitiesNavigator.Object, userPromptService, parentCtx.Object);

            return new UpdateTestContext
            {
                UtilitiesVM = utilitiesVM,
                OverlayVM = overlayVm,
                OperationOverlayController = overlayController,
                UserPromptService = userPromptService,
                DbServiceMock = dbService,
                CollectionExportServiceMock = collectionExportService,
                CardCollectionHostMock = parentCtx
            };
        }
        public static UpdateTestContextBuilder Builder => new();
    }
    public static class StatusTestDriver
    {
        public static Task WaitUntilPrimaryButtonTextAsync(OperationOverlayViewModel overlayVm, string expectedText, TimeSpan? timeout = null)
        {
            return WaitUntilAsync(() => overlayVm.PrimaryButtonText == expectedText, $"Timed out waiting for PrimaryButtonText == \"{expectedText}\"", timeout);
        }
        public static Task WaitUntilSecondaryButtonTextAsync(OperationOverlayViewModel overlayVm, string expectedText, TimeSpan? timeout = null)
        {
            return WaitUntilAsync(() => overlayVm.SecondaryButtonText == expectedText, $"Timed out waiting for SecondaryButtonText == \"{expectedText}\"", timeout);
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
        public static async Task WaitUntilAsync(Func<bool> condition, string timeoutMessage, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();

            while (!condition())
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException(timeoutMessage);
                }

                await Task.Delay(10);
            }
        }
    }
}

using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ViewModels;
using Moq;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUpdateViewModel : UpdateViewModel
    {
        public Task InternalUpdateTask => _internalUpdateTask!;
        private Task? _internalUpdateTask;

        public void TryCaptureUpdateTask()
        {
            _internalUpdateTask = UpdateDBAsync(); // legal: UpdateDBAsync is protected
        }

        public Action AfterUserConfirmedUpdate { get; set; } = () => { };
        public ManualResetEventSlim ConfirmReady { get; } = new();

        public TestableUpdateViewModel(
            ICardDatabaseManagementService dbService,
            StatusViewModel statusVM,
            IUiBlockable uiState,
            IAppRefresher appRefresher,
            Func<int> getMyCollectionCount)
            : base(dbService, statusVM, uiState, appRefresher, getMyCollectionCount)
        {
        }


        public static (TestableUpdateViewModel vm, StatusViewModel statusVM, Mock<ICardDatabaseManagementService> dbService) CreateTestableUpdateViewModel(
            OperationResult? backupResult = null,
            OperationResult? updateResult = null,
            Func<int>? getMyCollectionCount = null)
        {
            var dbService = new Mock<ICardDatabaseManagementService>();

            if (backupResult is not null)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(backupResult);
            }

            if (updateResult is not null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(updateResult);
            }

            var statusVM = new StatusViewModel();
            var uiState = new Mock<IUiBlockable>();
            var appRefresher = new Mock<IAppRefresher>();

            var updateVM = new TestableUpdateViewModel(
                dbService.Object,
                statusVM,
                uiState.Object,
                appRefresher.Object,
                getMyCollectionCount ?? (() => 5)
            );

            return (updateVM, statusVM, dbService);
        }

        public static void SimulatePrimaryButtonClick(StatusViewModel statusVM)
        {
            statusVM.PrimaryButtonCommand.Execute(null);
        }
    }
}

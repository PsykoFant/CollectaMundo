using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Utilities;
using CollectaMundo.ViewModels;
using Moq;
using System.Reflection;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableUpdateViewModel : UpdateViewModel
    {
        public Task? InternalUpdateTask { get; private set; }

        public TestableUpdateViewModel(ICardDatabaseManagementService dbService, StatusViewModel statusVM, IUiBlockable uiState, IAppRefresher appRefresher, Func<int> getMyCollectionCount) : base(dbService, statusVM, uiState, appRefresher, getMyCollectionCount)
        {
            UpdateDBCommand = new RelayCommand<object>(async _ =>
            {
                InternalUpdateTask = InvokeUpdateDBAsync(); // Calls private UpdateDBAsync()
                await InternalUpdateTask;
            });
        }
        public Task InvokeUpdateDBAsync() => (Task)typeof(UpdateViewModel).GetMethod("UpdateDBAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(this, null)!;

        public static (TestableUpdateViewModel vm, StatusViewModel statusVM, Mock<ICardDatabaseManagementService> dbService) CreateTestableUpdateViewModel(
            OperationResult? backupResult = null,
            OperationResult? updateResult = null,
            Func<int>? getMyCollectionCount = null)
        {
            var dbService = new Mock<ICardDatabaseManagementService>();

            if (backupResult is not null)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(backupResult);
            }

            if (updateResult is not null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(updateResult);
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
            var field = typeof(StatusViewModel).GetField("_primaryAction", BindingFlags.NonPublic | BindingFlags.Instance);
            var action = (Action<object?>)field!.GetValue(statusVM)!;
            action.Invoke(null);
        }
    }
}

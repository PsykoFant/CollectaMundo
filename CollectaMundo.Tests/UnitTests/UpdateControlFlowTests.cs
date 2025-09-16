using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Tests.TestUtils;
using Moq;
using System.Windows;


namespace CollectaMundo.Tests.UnitTests
{
    public class UpdateControlFlowTests
    {
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateSucceeds()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                updateResult: new OperationResult(OperationResultCode.Success, "Update complete"));

            // Act: start the update
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for "Go for it!" prompt
            while (statusVM.PrimaryButtonText != "   Go for it!   ")
            {
                await Task.Delay(1);
            }

            // Simulate user pressing the button
            statusVM.PrimaryButtonCommand.Execute(null);

            // Wait for actual task to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Database updated successfully!", statusVM.StatusLabel1);
            Assert.Equal("Your collection was backed up at mock-backup-path!", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateCancelledByUser()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                updateResult: new OperationResult(OperationResultCode.Success, "Update cancelled by user"));


            // Act: Start the command (this internally calls UpdateDBAsync and captures the task)
            // Start the command (this internally calls UpdateDBAsync and captures the task)
            updateVM.UpdateDBCommand.Execute(null);

            // Wait until the UpdateDBAsync method is actually running
            while (updateVM.InternalUpdateTask is null)
            {
                await Task.Delay(10);
            }

            // Simulate user cancel
            TestableUpdateViewModel.SimulatePrimaryButtonClick(statusVM);

            // Now wait for UpdateDBAsync to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Update canceled", statusVM.StatusLabel1);

            Assert.Equal("Download aborted. No files were imported.", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateFails()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                updateResult: new OperationResult(OperationResultCode.Error, "Boom!"));

            // Act: start the update
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for "Go for it!" prompt
            while (statusVM.PrimaryButtonText != "   Go for it!   ")
            {
                await Task.Delay(1);
            }

            // Simulate user pressing the button
            statusVM.PrimaryButtonCommand.Execute(null);

            // Wait for actual task to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Card database update failed!", statusVM.StatusLabel1);

            Assert.Equal("Boom!", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupFails_UpdateNotInvoked()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                backupResult: new OperationResult(OperationResultCode.Error, "Backup Boom!"),
                updateResult: null, // Won’t be used since update should not run
                getMyCollectionCount: () => 5
            );

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt
            while (statusVM.PrimaryButtonText != "   Go for it!   ")
            {
                await Task.Delay(1);
            }

            // Simulate user clicking the button
            TestableUpdateViewModel.SimulatePrimaryButtonClick(statusVM);

            // Wait for task to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Backup failed - aborting update...", statusVM.StatusLabel1);
            Assert.Equal("Backup Boom!", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            // Verify update was not invoked
            dbService.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupCancelled_UpdateNotInvoked()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                backupResult: new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download."),
                updateResult: null, // Won’t be used since update should not run
                getMyCollectionCount: () => 5
            );

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt
            while (statusVM.PrimaryButtonText != "   Go for it!   ")
            {
                await Task.Delay(1);
            }

            // Simulate user clicking the button
            TestableUpdateViewModel.SimulatePrimaryButtonClick(statusVM);

            // Wait for task to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Backup cancelled - aborting update...", statusVM.StatusLabel1);
            Assert.Equal("Update was cancelled by user during download.", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            // Verify update was not invoked
            dbService.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateSucceeds()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                updateResult: new OperationResult(OperationResultCode.Success, "Update complete"),
                getMyCollectionCount: () => 0 // triggers backup skip
            );

            // Act
            updateVM.UpdateDBCommand.Execute(null);
            while (statusVM.PrimaryButtonText != "   Go for it!   ")
            {
                await Task.Delay(1);
            }

            statusVM.PrimaryButtonCommand.Execute(null);
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Database updated successfully!", statusVM.StatusLabel1);
            Assert.DoesNotContain("backed up", statusVM.StatusLabel3);
            dbService.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateCancelledByUser()
        {
            // Arrange
            var (updateVM, statusVM, dbService) = TestableUpdateViewModel.CreateTestableUpdateViewModel(
                updateResult: new OperationResult(OperationResultCode.CancelledByUser, "Update cancelled by user"),
                getMyCollectionCount: () => 0 // triggers backup skip
            );

            // Act: Start the command (this internally calls UpdateDBAsync and captures the task)
            // Start the command (this internally calls UpdateDBAsync and captures the task)
            updateVM.UpdateDBCommand.Execute(null);

            // Wait until the UpdateDBAsync method is actually running
            while (updateVM.InternalUpdateTask is null)
            {
                await Task.Delay(10);
            }

            // Simulate user cancel
            TestableUpdateViewModel.SimulatePrimaryButtonClick(statusVM);

            // Now wait for UpdateDBAsync to complete
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Update canceled", statusVM.StatusLabel1);

            Assert.Equal("Download aborted. No files were imported.", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
            dbService.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

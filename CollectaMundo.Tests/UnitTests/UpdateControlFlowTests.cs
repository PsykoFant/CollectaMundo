using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Tests.TestUtils;
using Moq;
using System.Diagnostics;
using System.Windows;


namespace CollectaMundo.Tests.UnitTests
{
    public class UpdateControlFlowTests
    {
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateSucceeds()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete"))
                .WithCollectionCount(5) // Simulate non-empty collection
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            // Act: start the update command
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(statusVM);

            // Await the internal task
            await updateVM.InternalUpdateTask!;

            // Assert final state
            Assert.Equal("Database updated successfully!", statusVM.StatusLabel1);
            Assert.Equal("Your collection was backed up at mock-backup-path", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            // Verify orchestration calls
            context.DbServiceMock.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupCancelled_UpdateNotInvoked()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download."))
                .WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete"))
                .WithCollectionCount(5) // Simulate non-empty collection
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Simulate user confirmation
            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(statusVM);

            // Wait until ViewModel finishes processing cancellation
            var timeout = TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();
            while (!updateVM.InternalUpdateTask!.IsCompleted)
            {
                if (sw.Elapsed > timeout)
                    throw new TimeoutException("Timed out waiting for ViewModel to complete backup cancellation flow.");

                await Task.Delay(10);
            }

            Debug.WriteLine($"[Test] Backup cancellation flow completed.");

            // Assert
            Assert.True(updateVM.InternalUpdateTask!.IsCompletedSuccessfully);
            Assert.Equal("Backup cancelled - aborting update...", statusVM.StatusLabel1);
            Assert.Equal("Update was cancelled by user during download.", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupFails_UpdateNotInvoked()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Error, "Backup Boom!"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(statusVM);

            // Wait until the backup flow completes
            await updateVM.InternalUpdateTask!;

            // Task completed successfully, but no update was triggered
            Assert.True(updateVM.InternalUpdateTask!.IsCompletedSuccessfully);
            Assert.Equal("Backup failed - aborting update...", statusVM.StatusLabel1);
            Assert.Equal("Backup Boom!", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            // Ensure update orchestration was never invoked
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateFails()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithUpdateResult(new OperationResult(OperationResultCode.Error, "Boom!"))
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(statusVM);

            // Await task completion
            await updateVM.InternalUpdateTask!;

            // Assert failure UI state
            Assert.Equal("Card database update failed!", statusVM.StatusLabel1);
            Assert.Equal("Boom!", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            // Verify both backup and update were called
            context.DbServiceMock.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateCancelledByUser()
        {
            var orchestratorStarted = new ManualResetEventSlim();

            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithCustomUpdateOrchestrator(async (delay, ct) =>
                {
                    orchestratorStarted.Set(); // signal inside orchestrator
                    try
                    {
                        await Task.Delay(5000, ct);
                        return new OperationResult(OperationResultCode.Success, "Should not reach");
                    }
                    catch (OperationCanceledException)
                    {
                        return new OperationResult(OperationResultCode.CancelledByUser, "Download aborted. No files were imported.");
                    }
                })
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(statusVM);

            while (updateVM.InternalUpdateTask is null)
                await Task.Delay(10);

            await StatusTestDriver.WaitUntilButtonTextAsync(statusVM, "   Cancel   ");

            orchestratorStarted.Wait(); // Wait until orchestrator is running

            StatusTestDriver.ClickPrimaryButton(statusVM); // Cancel
            await updateVM.InternalUpdateTask;

            Assert.Equal("Update canceled", statusVM.StatusLabel1);
            Assert.Equal("Download aborted. No files were imported.", statusVM.StatusLabel3);
            Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateSucceeds()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete"))
                .WithCollectionCount(0) // ✅ Triggers backup skip
                .Build();

            // Act
            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilButtonTextAsync(context.StatusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(context.StatusVM);

            await context.UtilitiesVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Database updated successfully!", context.StatusVM.StatusLabel1);
            Assert.DoesNotContain("backed up", context.StatusVM.StatusLabel3);
            context.DbServiceMock.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateCancelledByUser()
        {
            var orchestratorStarted = new ManualResetEventSlim();

            var context = UpdateTestContextBuilder.Builder
                .WithCollectionCount(0) // Skip backup logic in VM
                .WithSkippedBackup()    // Throw if backup is mistakenly called
                .WithCustomUpdateOrchestrator(async (_, ct) =>
                {
                    orchestratorStarted.Set(); // ⏳ signal when update is running
                    try
                    {
                        await Task.Delay(5000, ct); // simulate work
                        return new OperationResult(OperationResultCode.Success, "Should not reach");
                    }
                    catch (OperationCanceledException)
                    {
                        return new OperationResult(OperationResultCode.CancelledByUser, "Download aborted. No files were imported.");
                    }
                })
                .Build();

            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilButtonTextAsync(context.StatusVM, "   Start card database update!   ");
            StatusTestDriver.ClickPrimaryButton(context.StatusVM);

            await StatusTestDriver.WaitUntilButtonTextAsync(context.StatusVM, "   Cancel   ");
            orchestratorStarted.Wait(); // ⏳ ensure task is running

            StatusTestDriver.ClickPrimaryButton(context.StatusVM); // Cancel
            await context.UtilitiesVM.InternalUpdateTask;

            // Assert
            Assert.Equal("Update canceled", context.StatusVM.StatusLabel1);
            Assert.Equal("Download aborted. No files were imported.", context.StatusVM.StatusLabel3);
            Assert.Equal("  OK  ", context.StatusVM.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, context.StatusVM.PrimaryButtonVisibility);

            context.DbServiceMock.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task BackupCollection_EmptyCollection_ShowsEmptyMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithCollectionCount(0)
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Your collection is empty - nothing to back up", statusVM.StatusLabel3);
            Assert.Equal("   Oh ... I guess that makes sense...   ", statusVM.PrimaryButtonText);
        }
        [Fact]
        public async Task BackupCollection_UserCancels_AbortsOperation()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "path"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;
            var userPromptService = context.UserPromptService;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(statusVM, "   Start backup   ");

            // Simulate user not confirming
            userPromptService.CancelPendingPrompt(); // simulate close or navigation away

            await context.UtilitiesVM.InternalUpdateTask!;

            // No result should be shown
            Assert.DoesNotContain("Backup complete", statusVM.StatusLabel1);
            context.DbServiceMock.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task BackupCollection_BackupSucceeds_ShowsSuccessMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(statusVM, "   Start backup   ");
            StatusTestDriver.ClickSecondaryButton(statusVM); // Confirm

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Backup complete!", statusVM.StatusLabel1);
            Assert.Equal("Backup created successfully at mock-backup-path", statusVM.StatusLabel3);
            Assert.Equal("   Awesome!   ", statusVM.PrimaryButtonText);
        }
        [Fact]
        public async Task BackupCollection_BackupFails_ShowsErrorMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Error, "Write access denied"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var statusVM = context.StatusVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(statusVM, "   Start backup   ");
            StatusTestDriver.ClickSecondaryButton(statusVM); // Confirm

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Error: Write access denied", statusVM.StatusLabel3);
            Assert.Equal("   Ok :-/   ", statusVM.PrimaryButtonText);
        }
    }
}

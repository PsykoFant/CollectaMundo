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
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(overlay);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Database updated successfully!", overlay.Headline);
            Assert.Equal("Your collection was backed up at mock-backup-path", overlay.Detail);

            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_BackupCancelled_UpdateNotInvoked()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(
                    OperationResultCode.CancelledByUser,
                    "Update was cancelled by user during download."))
                .WithUpdateResult(new OperationResult(
                    OperationResultCode.Success,
                    "Update complete"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Simulate user confirmation
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(overlay);

            // Wait until ViewModel finishes processing cancellation
            var timeout = TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();
            while (!updateVM.InternalUpdateTask!.IsCompleted)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException("Timed out waiting for ViewModel to complete backup cancellation flow.");
                }

                await Task.Delay(10);
            }

            Debug.WriteLine("[Test] Backup cancellation flow completed.");

            // Assert
            Assert.True(updateVM.InternalUpdateTask.IsCompletedSuccessfully);
            Assert.Equal("Backup cancelled - aborting update...", overlay.Headline);
            Assert.Equal("Update was cancelled by user during download.", overlay.Detail);
            Assert.Equal("  OK  ", overlay.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, overlay.PrimaryButtonVisibility);

            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
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
            var overlay = context.Overlay;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(overlay);

            // Wait until the backup flow completes
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.True(updateVM.InternalUpdateTask.IsCompletedSuccessfully);
            Assert.Equal("Backup failed - aborting update...", overlay.Headline);
            Assert.Equal("Backup Boom!", overlay.Detail);
            Assert.Equal("  OK  ", overlay.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, overlay.PrimaryButtonVisibility);

            // Ensure update orchestration was never invoked
            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
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
            var overlay = context.Overlay;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(overlay);

            // Await task completion
            await updateVM.InternalUpdateTask!;

            // Assert failure UI state
            Assert.Equal("Card database update failed!", overlay.Headline);
            Assert.Equal("Boom!", overlay.Detail);
            Assert.Equal("  OK  ", overlay.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, overlay.PrimaryButtonVisibility);

            // Verify both backup and update were called
            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateCancelledByUser()
        {
            var orchestratorStarted = new ManualResetEventSlim();

            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithCustomUpdateOrchestrator(async ct =>
                {
                    orchestratorStarted.Set();
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
            var overlay = context.Overlay;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(overlay);

            while (updateVM.InternalUpdateTask is null)
            {
                await Task.Delay(10);
            }

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlay,
                "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(overlay);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Update canceled", overlay.Headline);
            Assert.Equal("Download aborted. No files were imported.", overlay.Detail);
            Assert.Equal("  OK  ", overlay.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, overlay.PrimaryButtonVisibility);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateSucceeds()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete"))
                .WithCollectionCount(0)
                .Build();

            // Act
            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                context.Overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(context.Overlay);

            await context.UtilitiesVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Database updated successfully!", context.Overlay.Headline);
            Assert.DoesNotContain("backed up", context.Overlay.Detail);
            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateCancelledByUser()
        {
            var orchestratorStarted = new ManualResetEventSlim();

            var context = UpdateTestContextBuilder.Builder
                .WithCollectionCount(0)
                .WithSkippedBackup()
                .WithCustomUpdateOrchestrator(async ct =>
                {
                    orchestratorStarted.Set();
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

            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                context.Overlay,
                "   Start card database update!   ");

            StatusTestDriver.Confirm(context.Overlay);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                context.Overlay,
                "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(context.Overlay);

            await context.UtilitiesVM.InternalUpdateTask;

            Assert.Equal("Update canceled", context.Overlay.Headline);
            Assert.Equal("Download aborted. No files were imported.", context.Overlay.Detail);
            Assert.Equal("  OK  ", context.Overlay.PrimaryButtonText);
            Assert.Equal(Visibility.Visible, context.Overlay.PrimaryButtonVisibility);

            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Never);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task BackupCollection_EmptyCollection_ShowsEmptyMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithCollectionCount(0)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;

            updateVM.BackupCollectionCommand.Execute(null);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Your collection is empty - nothing to back up", overlay.Detail);
            Assert.Equal("   Oh ... I guess that makes sense...   ", overlay.PrimaryButtonText);
        }

        [Fact]
        public async Task BackupCollection_UserCancels_AbortsOperation()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "path"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;
            var userPromptService = context.UserPromptService;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlay,
                "   Start backup   ");

            // Simulate user not confirming
            userPromptService.CancelActivePrompt();

            await context.UtilitiesVM.InternalUpdateTask!;

            // No result should be shown
            Assert.DoesNotContain("Backup complete", overlay.Headline);
            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task BackupCollection_BackupSucceeds_ShowsSuccessMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlay,
                "   Start backup   ");

            StatusTestDriver.ClickSecondaryButton(overlay);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Backup complete!", overlay.Headline);
            Assert.Equal("Backup created successfully at mock-backup-path", overlay.Detail);
            Assert.Equal("   Awesome!   ", overlay.PrimaryButtonText);
        }

        [Fact]
        public async Task BackupCollection_BackupFails_ShowsErrorMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Error, "Write access denied"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlay = context.Overlay;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlay,
                "   Start backup   ");

            StatusTestDriver.ClickSecondaryButton(overlay);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Error: Write access denied", overlay.Detail);
            Assert.Equal("   Ok :-/   ", overlay.PrimaryButtonText);
        }

    }
}

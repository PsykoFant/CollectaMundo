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
            var overlayVm = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Database updated successfully!", overlayVm.Headline);
            Assert.Equal("Your collection was backed up at mock-backup-path", overlayVm.Detail);

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
            var overlayVm = context.OverlayVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Simulate user confirmation
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVm);

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
            Assert.Equal("Backup cancelled - aborting update...", overlayVm.Headline);
            Assert.Equal("Update was cancelled by user during download.", overlayVm.Detail);
            Assert.Equal("  OK  ", overlayVm.PrimaryButtonText);
            Assert.Equal(true, overlayVm.IsPrimaryButtonVisible);

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
            var overlayVM = context.OverlayVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            // Wait until the backup flow completes
            await updateVM.InternalUpdateTask!;

            // Assert
            Assert.True(updateVM.InternalUpdateTask.IsCompletedSuccessfully);
            Assert.Equal("Backup failed - aborting update...", overlayVM.Headline);
            Assert.Equal("Backup Boom!", overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.Equal(true, overlayVM.IsPrimaryButtonVisible);

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
            var overlayVM = context.OverlayVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            // Await task completion
            await updateVM.InternalUpdateTask!;

            // Assert failure UI state
            Assert.Equal("Card database update failed!", overlayVM.Headline);
            Assert.Equal("Boom!", overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.Equal(true, overlayVM.IsPrimaryButtonVisible);

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
            var overlayVm = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            while (updateVM.InternalUpdateTask is null)
            {
                await Task.Delay(10);
            }

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Update canceled", overlayVm.Headline);
            Assert.Equal("Download aborted. No files were imported.", overlayVm.Detail);
            Assert.Equal("  OK  ", overlayVm.PrimaryButtonText);
            Assert.Equal(true, overlayVm.IsPrimaryButtonVisible);

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

            var overlayVM = context.OverlayVM;

            // Act
            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await context.UtilitiesVM.InternalUpdateTask!;

            // Assert
            Assert.Equal("Database updated successfully!", overlayVM.Headline);
            Assert.DoesNotContain("backed up", overlayVM.Detail);
            context.DbServiceMock.Verify(
                s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateCancelledByUser()
        {
            // Arrange
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

            var overlayVM = context.OverlayVM;

            // Act
            context.UtilitiesVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await context.UtilitiesVM.InternalUpdateTask;

            // Assert
            Assert.Equal("Update canceled", overlayVM.Headline);
            Assert.Equal("Download aborted. No files were imported.", overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.Equal(true, overlayVM.IsPrimaryButtonVisible);

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
            var overlayVm = context.OverlayVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Your collection is empty - nothing to back up", overlayVm.Detail);
            Assert.Equal("   Oh ... I guess that makes sense...   ", overlayVm.PrimaryButtonText);
        }

        [Fact]
        public async Task BackupCollection_UserCancels_AbortsOperation()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "path"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVm = context.OverlayVM;
            var userPromptService = context.UserPromptService;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlayVm,
                "   Start backup   ");

            // Simulate user not confirming
            userPromptService.DisposeActivePrompt();

            await context.UtilitiesVM.InternalUpdateTask!;

            // No result should be shown
            Assert.DoesNotContain("Backup complete", overlayVm.Headline);
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
            var overlayVm = context.OverlayVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlayVm,
                "   Start backup   ");

            StatusTestDriver.ClickSecondaryButton(overlayVm);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Backup complete!", overlayVm.Headline);
            Assert.Equal("Backup created successfully at mock-backup-path", overlayVm.Detail);
            Assert.Equal("   Awesome!   ", overlayVm.PrimaryButtonText);
        }

        [Fact]
        public async Task BackupCollection_BackupFails_ShowsErrorMessage()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Error, "Write access denied"))
                .WithCollectionCount(5)
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVm = context.OverlayVM;

            updateVM.BackupCollectionCommand.Execute(null);

            await StatusTestDriver.WaitUntilSecondaryButtonTextAsync(
                overlayVm,
                "   Start backup   ");

            StatusTestDriver.ClickSecondaryButton(overlayVm);

            await context.UtilitiesVM.InternalUpdateTask!;

            Assert.Equal("Error: Write access denied", overlayVm.Detail);
            Assert.Equal("   Ok :-/   ", overlayVm.PrimaryButtonText);
        }

    }
}

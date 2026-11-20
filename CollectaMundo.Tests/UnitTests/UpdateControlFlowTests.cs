using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.Tests.TestUtils;
using Moq;


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
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVm = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVm, "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Database updated successfully!", overlayVm.Headline);
            Assert.Equal("Your collection was backed up at mock-backup-path", overlayVm.Detail);

            context.CollectionExportServiceMock.Verify(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()), Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_BackupCancelled_UpdateNotInvoked()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download."))
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVm = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVm, "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Backup cancelled - update aborted.", overlayVm.Headline);
            Assert.Equal("Update was cancelled by user during download.", overlayVm.Detail);
            Assert.Equal("  OK  ", overlayVm.PrimaryButtonText);
            Assert.True(overlayVm.IsPrimaryButtonVisible);

            context.CollectionExportServiceMock.Verify(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDbAsync_BackupFails_UpdateNotInvoked()
        {
            // Arrange
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Error, "Backup Boom!"))
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVM = context.OverlayVM;

            // Act
            updateVM.UpdateDBCommand.Execute(null);

            // Wait for prompt and simulate user confirming update
            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVM, "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            // Wait until the backup flow completes
            await updateVM.InternalUpdateTask;

            // Assert
            Assert.True(updateVM.InternalUpdateTask.IsCompletedSuccessfully);
            Assert.Equal("Backup failed - update aborted.", overlayVM.Headline);
            Assert.Equal("Backup Boom!", overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.Equal(true, overlayVM.IsPrimaryButtonVisible);

            context.CollectionExportServiceMock.Verify(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDbAsync_BackupSucceeds_UpdateFails()
        {
            var context = UpdateTestContextBuilder.Builder
                .WithBackupResult(new OperationResult(OperationResultCode.Success, "mock-backup-path"))
                .WithUpdateResult(new OperationResult(OperationResultCode.Error, "Boom!"))
                .Build();

            var updateVM = context.UtilitiesVM;
            var overlayVM = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVM,
                "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Card database update failed!", overlayVM.Headline);
            Assert.Equal(
                $"Boom!{Environment.NewLine}Your collection was backed up at mock-backup-path",
                overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.True(overlayVM.IsPrimaryButtonVisible);

            context.CollectionExportServiceMock.Verify(
                s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
                        return new OperationResult(
                            OperationResultCode.CancelledByUser,
                            "Download aborted. No files were imported.");
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

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(overlayVm);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Update canceled", overlayVm.Headline);
            Assert.Equal("Download aborted. No files were imported. Your collection was backed up at mock-backup-path", overlayVm.Detail);
            Assert.True(overlayVm.IsPrimaryButtonVisible);

            context.CollectionExportServiceMock.Verify(
                s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_UpdateSucceeds()
        {
            var context = UpdateTestContextBuilder.Builder.WithEmptyCollection().WithUpdateResult(new OperationResult(OperationResultCode.Success, "Update complete")).Build();

            var updateVM = context.UtilitiesVM;
            var overlayVM = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVM, "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Database updated successfully!", overlayVM.Headline);
            Assert.Equal("Your collection is empty; no backup was needed.", overlayVM.Detail);
            context.CollectionExportServiceMock.Verify(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_EmptyCollection_UpdateCancelledByUser()
        {
            var orchestratorStarted = new ManualResetEventSlim();

            var context = UpdateTestContextBuilder.Builder.WithEmptyCollection().WithCustomUpdateOrchestrator(async ct =>
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
                }).Build();

            var updateVM = context.UtilitiesVM;
            var overlayVM = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVM, "   Start card database update!   ");

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(overlayVM, "   Cancel   ");

            orchestratorStarted.Wait();

            StatusTestDriver.ClickPrimaryButton(overlayVM);

            await updateVM.InternalUpdateTask;

            Assert.Equal("Update canceled", overlayVM.Headline);
            Assert.Equal($"Download aborted. No files were imported. Your collection is empty; no backup was needed.", overlayVM.Detail);
            Assert.Equal("  OK  ", overlayVM.PrimaryButtonText);
            Assert.True(overlayVM.IsPrimaryButtonVisible);

            context.CollectionExportServiceMock.Verify(s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()), Times.Once);
            context.DbServiceMock.Verify(s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDbAsync_UserDeclinesConfirmation_DoesNotBackupOrUpdate()
        {
            var context = UpdateTestContextBuilder.Builder.Build();
            var updateVM = context.UtilitiesVM;
            var overlayVm = context.OverlayVM;

            updateVM.UpdateDBCommand.Execute(null);

            await StatusTestDriver.WaitUntilPrimaryButtonTextAsync(
                overlayVm,
                "   Start card database update!   ");

            context.UserPromptService.DisposeActivePrompt();

            await updateVM.InternalUpdateTask;

            context.CollectionExportServiceMock.Verify(
                s => s.ExportFullCollectionBackupAsync(It.IsAny<CancellationToken>()),
                Times.Never);

            context.DbServiceMock.Verify(
                s => s.UpdateDbPrepOrchestrator(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}

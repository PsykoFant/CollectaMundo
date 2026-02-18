using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Import.ImportSteps;
using FluentAssertions;
using System.IO;

namespace CollectaMundo.Tests.ScenarioTests
{

    public sealed class ImportScenarioTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        #region Test class setup
        private readonly InMemoryDatabaseFixture _fx = fx;

        private IDbConnectionFactory _dbFactory = null!;
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }
        #endregion

        [Fact]
        public async Task Import_full_flow_happy_path()
        {
            // =====================================================
            // Arrange – infrastructure & initial state
            // =====================================================

            var csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest1.csv");

            File.Exists(csvPath).Should().BeTrue();

            var prompt = new TestPromptService(csvPath);
            var picker = new TestFileSystemPicker(csvPath);

            var (vm, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: prompt, filePickerOverride: picker);

            _mainVM = vm;
            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().HaveCount(22);

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            await importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep01_StartViewModel && importVM.ProgressHeadline == "The Import Wizard",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 6 should be active and progress label updated");
            step1.PrimaryActionButtonText.Should().Contain("Let's go");

            var step1Result = await step1.OnPrimaryAction(); // Parse CSV

            // Assert step 1 completed successfully
            step1Result.Code.Should().Be(OperationResultCode.Success);
            importVM.ImportCardList.Should().HaveCount(10);

            // =====================================================
            // Step 2 – ID column mapping
            // =====================================================

            var step2 = (ImportStep02_IdMappingViewModel)importVM.CurrentStepViewModel;
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep02_IdMappingViewModel>();
            importVM.ProgressStep.Should().Be("ID column mapping");
            step2.PrimaryActionButtonText.Should().Contain("Proceed");

            // Assert CSV headers available
            step2.IdMappings.Should().HaveCount(1);
            var mapping = step2.IdMappings[0];

            mapping.CsvHeaders.Should().HaveCount(18);
            mapping.SelectedCsvHeader.Should().NotBeNull();
            mapping.SelectedDatabaseField.Should().NotBeNull();
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Simulate clearing mapping
            mapping.SelectedCsvHeader = null;
            mapping.SelectedDatabaseField = null;

            // Assert cleared state
            mapping.SelectedCsvHeader.Should().BeNull();
            mapping.SelectedDatabaseField.Should().BeNull();

            // CanExecute should now be false
            step2.CanExecutePrimaryAction.Should().BeFalse();

            // Map to MCM Id
            mapping.SelectedCsvHeader = "MCM ID";
            mapping.SelectedDatabaseField = "mcmId";

            // CanExecute should now be true
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Proceed to map using Id
            var step2Result = await step2.OnPrimaryAction();

            // Assert step 2 completed successfully
            step2Result.Code.Should().Be(OperationResultCode.Success);

            // After ID mapping, we should have 3 cards with UUIDs (the ones that had MCM IDs in the CSV)
            importVM.ImportCardList.Count(HasUuid).Should().Be(4);


            // =====================================================
            // Step 3 – Name & set mapping
            // =====================================================
            var step3 = (ImportStep03_NameSetMappingViewModel)importVM.CurrentStepViewModel;
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep03_NameSetMappingViewModel>();
            importVM.ProgressStep.Should().Be("Name and set mapping");
            step2.PrimaryActionButtonText.Should().Contain("Proceed");

            step3.NameSetMappings.Should().HaveCount(3);
            var nameSetmapping = step3.NameSetMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            nameSetmapping[0].FieldToMap.Should().Be(ImportField.CardName);
            nameSetmapping[1].FieldToMap.Should().Be(ImportField.SetName);
            nameSetmapping[2].FieldToMap.Should().Be(ImportField.SetCode);
            nameSetmapping[0].CsvHeaders.Should().HaveCount(18);

            // Assert CSV headers pre-selected
            nameSetmapping[0].SelectedCsvHeader.Should().Be("CardName");
            nameSetmapping[1].SelectedCsvHeader.Should().Be("Set");
            nameSetmapping[2].SelectedCsvHeader.Should().Be("Set Code");

            // Proceed to map using Name & Set
            var step3Result = await step3.OnPrimaryAction();

            // Assert step 3 completed successfully
            step3Result.Code.Should().Be(OperationResultCode.Success);

            // After Name andSet mapping, we should have 3 cards with UUIDs (the ones that had MCM IDs in the CSV)
            importVM.ImportCardList.Count(HasUuid).Should().Be(8);
            importVM.ImportCardList.Count(HasUuids).Should().Be(1); // One card should have multiple UUIDs due to multiple matches

            // =====================================================
            // Step 4 – Multiple UUID matches
            // =====================================================
            var step4 = (ImportStep04_MultipleUuidsViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep04_MultipleUuidsViewModel && importVM.ProgressStep == "Multiple versions found",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 4 should be active and progress label updated");
            step4.PrimaryActionButtonText.Should().Contain("Proceed");

            // Check that MultipleUuidsItem object is correctly populated with the expected card that has multiple UUID matches
            step4.MultipleUuidItems.Should().HaveCount(1);
            step4.CanExecutePrimaryAction.Should().BeFalse();
            var multipleUuidItem = step4.MultipleUuidItems[0];
            multipleUuidItem.Name.Should().Contain("Prismatic Ending");
            multipleUuidItem.VersionedUuids.Should().HaveCount(2);
            multipleUuidItem.SelectedUuid.Should().BeNull();
            multipleUuidItem.VersionedUuids[0].DisplayText.Should().Be("Version 1");
            multipleUuidItem.VersionedUuids[1].DisplayText.Should().Be("Version 2");

            // Choose version 2 and proceed
            multipleUuidItem.SelectedUuid = "bafac74c-f4f8-5c71-8a6b-0bd02c536c47";
            step4.CanExecutePrimaryAction.Should().BeTrue();
            var step4Result = await step4.OnPrimaryAction();

            // Assert step 4 completed successfully
            step4Result.Code.Should().Be(OperationResultCode.Success);

            // After choosing the correct UUID for the card with multiple matches, we should now have 7 cards with UUIDs in total (the 3 from ID mapping + the 3 from Name/Set mapping + one we just resolved)
            importVM.ImportCardList.Count(HasUuid).Should().Be(9);
            importVM.ImportCardList.Count(HasUuids).Should().Be(0); // We should have resolved the multiple UUIDs, so none should have multiple anymore

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(5);
            var addtionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            addtionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            addtionalMappings[4].FieldToMap.Should().Be(ImportField.CardsForTrade);
            addtionalMappings[0].CsvHeaders.Should().HaveCount(18);

            // Assert CSV headers pre-selected
            addtionalMappings[0].SelectedCsvHeader.Should().Be("Condition");
            addtionalMappings[1].SelectedCsvHeader.Should().Be("Printing");
            addtionalMappings[2].SelectedCsvHeader.Should().Be("Language");
            addtionalMappings[3].SelectedCsvHeader.Should().Be("Quantity");
            addtionalMappings[4].SelectedCsvHeader.Should().Be("For sale");

            // Proceed to next step
            var step5Result = await step5.OnPrimaryAction();

            // Assert step 5 completed successfully
            step5Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 6 - Conditions mapping
            // =====================================================
            var step6 = (ImportStep06_ConditionsMappingViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep06_ConditionsMappingViewModel && importVM.ProgressStep == "Condition value mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 6 should be active and progress label updated");
            step6.PrimaryActionButtonText.Should().Contain("Proceed");

            step6.ConditionMappings.Should().HaveCount(5);
            var conditionMappings = step6.ConditionMappings;

            // Check ConditionMappingItem object is correctly initialized with guesses
            conditionMappings[0].CsvValue.Should().Be("Near Mint");
            conditionMappings[0].SelectedCardSetValue.Should().Be("Near Mint");
            conditionMappings[1].CsvValue.Should().Be("Nearly sublime");
            conditionMappings[1].SelectedCardSetValue.Should().Be("Near Mint");
            conditionMappings[2].CsvValue.Should().Be("Bad");
            conditionMappings[2].SelectedCardSetValue.Should().Be("Near Mint");
            conditionMappings[3].CsvValue.Should().Be("Good");
            conditionMappings[3].SelectedCardSetValue.Should().Be("Good");
            conditionMappings[4].CsvValue.Should().Be("Mint");
            conditionMappings[4].SelectedCardSetValue.Should().Be("Mint");

            // Simulate clearing a mapping (should use default value for that condition, which is "Near Mint")
            conditionMappings[1].SelectedCardSetValue = null;

            // Proceed to next step
            var step6Result = await step6.OnPrimaryAction();

            // Assert step 6 completed successfully
            step6Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 7 - Finish mapping
            // =====================================================
            var step7 = (ImportStep07_FinishMappingViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep07_FinishMappingViewModel && importVM.ProgressStep == "Finish value mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 7 should be active and progress label updated");
            step7.PrimaryActionButtonText.Should().Contain("Proceed");

            step7.FinishMappings.Should().HaveCount(4);
            var finishMappings = step7.FinishMappings;

            // Check FinishMappingItem object is correctly initialized with guesses
            finishMappings[0].CsvValue.Should().Be("Normal");
            finishMappings[0].SelectedCardSetValue.Should().Be("nonfoil");
            finishMappings[1].CsvValue.Should().Be("nonfoil");
            finishMappings[1].SelectedCardSetValue.Should().Be("nonfoil");
            finishMappings[2].CsvValue.Should().Be("Shiny");
            finishMappings[2].SelectedCardSetValue.Should().Be("foil");
            finishMappings[3].CsvValue.Should().Be("nothing");
            finishMappings[3].SelectedCardSetValue.Should().Be("nonfoil");

            // Simulate clearing a mapping (should use default value for that finish, which is "nonfoil")
            finishMappings[3].SelectedCardSetValue = null;

            // Proceed to next step
            var step7Result = await step7.OnPrimaryAction();

            // Assert step 7 completed successfully
            step7Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 8 - Language mapping
            // =====================================================

            var step8 = (ImportStep08_LanguageMappingViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep08_LanguageMappingViewModel && importVM.ProgressStep == "Language value mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 8 should be active and progress label updated");
            step8.PrimaryActionButtonText.Should().Contain("Proceed");

            step8.LanguageMappings.Should().HaveCount(5);
            var languageMappings = step8.LanguageMappings;

            // Check FinishMappingItem object is correctly initialized with guesses
            languageMappings[0].CsvValue.Should().Be("xxxx");
            languageMappings[0].SelectedCardSetValue.Should().Be("English");
            languageMappings[1].CsvValue.Should().Be("French");
            languageMappings[1].SelectedCardSetValue.Should().Be("French");
            languageMappings[2].CsvValue.Should().Be("English");
            languageMappings[2].SelectedCardSetValue.Should().Be("English");
            languageMappings[3].CsvValue.Should().Be("Dansk");
            languageMappings[3].SelectedCardSetValue.Should().Be("English");
            languageMappings[4].CsvValue.Should().Be("Spanish");
            languageMappings[4].SelectedCardSetValue.Should().Be("Spanish");

            // Simulate clearing a mapping (should use default value for that language, which is "English")
            languageMappings[0].SelectedCardSetValue = null;

            // Proceed to next step
            var step8Result = await step8.OnPrimaryAction();

            // Assert step 7 completed successfully
            step8Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 9 - Summary and confirmation
            // =====================================================
            var step9 = (ImportStep09_SummaryViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep09_SummaryViewModel && importVM.ProgressStep == "Summary and confirmation",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 9 should be active and progress label updated");
            step9.PrimaryActionButtonText.Should().Contain("Start the import...");
            step9.SecondaryActionButtonText.Should().Contain("Save unrecognized items");

            var summary = step9.Summary;

            // Check totals
            //summary.ReadyToImportCount.Should().Be(7); // 7 cards should be ready to import with UUIDs
            //summary.TotalCardsToAdd.Should().Be(14); // Sum of quantities of all cards to import
            //summary.UnableToImportCount.Should().Be(1); // 1 card should not be able to import

            //// Check field mappings are correctly displayed in summary
            //summary.FieldMappings[0].CsvHeader.Should().Be("Condition");
            //summary.FieldMappings[1].CsvHeader.Should().Be("Printing");
            //summary.FieldMappings[2].CsvHeader.Should().Be("Language");
            //summary.FieldMappings[3].CsvHeader.Should().Be("Quantity");
            //summary.FieldMappings[4].CsvHeader.Should().Be("For sale");

            //// Spot check value mappings 
            //summary.ValueMappings[0].Field.Should().Be(ImportField.Condition);
            //summary.ValueMappings[0].CsvValue.Should().Be("Near Mint");
            //summary.ValueMappings[0].MappedValue.Should().Be("Near Mint");
            //summary.ValueMappings[1].Field.Should().Be(ImportField.Condition);
            //summary.ValueMappings[1].CsvValue.Should().Be("Nearly sublime");
            //summary.ValueMappings[1].MappedValue.Should().Be("(blank -> Near Mint)");
            //summary.ValueMappings[7].Field.Should().Be(ImportField.CardFinish);
            //summary.ValueMappings[7].CsvValue.Should().Be("Shiny");
            //summary.ValueMappings[7].MappedValue.Should().Be("foil");
            //summary.ValueMappings[8].Field.Should().Be(ImportField.CardFinish);
            //summary.ValueMappings[8].CsvValue.Should().Be("nothing");
            //summary.ValueMappings[8].MappedValue.Should().Be("(blank -> nonfoil)");

            //summary.UnimportableItems.Should().HaveCount(1);
            //summary.UnimportableItems[0].CardName.Should().Contain("Does not exist");

            // Proceed with the import
            var step9Result = await step9.OnPrimaryAction();

            // Assert that the final import completed successfully
            step9Result.Code.Should().Be(OperationResultCode.Success);

            //_mainVM.MyCollectionVM.Cards.Should().HaveCount(26);

            // =====================================================
            // ...
            // Continue same pattern up to Step 9
            // =====================================================
        }

        #region Helpers
        static bool HasUuid(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidImportField", out var uuid)
                   && !string.IsNullOrWhiteSpace(uuid);
        }
        static bool HasUuids(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidsImportField", out var uuid)
                   && !string.IsNullOrWhiteSpace(uuid);
        }
        private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, string? because = null)
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(10);
            }

            // One last check before failing (helps if it flips right at the end)
            condition().Should().BeTrue(because ?? "condition was not met before timeout");
        }
        #endregion
    }
    internal sealed class TestFileSystemPicker : IFileSystemPicker
    {
        private readonly string _pathToReturn;

        public TestFileSystemPicker(string pathToReturn)
        {
            _pathToReturn = pathToReturn;
        }

        public string? PickFile(
            string title,
            string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*")
        {
            return _pathToReturn;
        }

        public string? PickFolder(string title, string? initialPath = null)
        {
            throw new NotSupportedException("PickFolder is not used in import tests.");
        }

        public string? PickSaveFile(string title, string defaultFileName, string filter)
        {
            throw new NotSupportedException("PickSaveFile is not used in import tests.");
        }
    }

}

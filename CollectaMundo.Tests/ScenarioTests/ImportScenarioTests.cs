using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Import.ImportSteps;
using FluentAssertions;
using System.Data.SQLite;
using System.IO;

namespace CollectaMundo.Tests.ScenarioTests
{
    #region Ímport Scenario 1
    public sealed class ImportScenarioTests1(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        readonly static string csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest1.csv");

        private readonly InMemoryDatabaseFixture _fx = fx;
        private IDbConnectionFactory _dbFactory = null!;
        private readonly TestPromptService _prompt = new();
        private readonly TestFileSystemPicker _picker = new(csvPath);
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: _prompt, filePickerOverride: _picker);
        }

        [Fact]
        public async Task Import_scenario_1()
        {
            File.Exists(csvPath).Should().BeTrue();

            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().HaveCount(22);

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep01_StartViewModel && importVM.ProgressHeadline == "The Import Wizard",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 1 should be active and progress label updated");
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
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(4);

            // =====================================================
            // Step 3 – Name & set mapping
            // =====================================================
            var step3 = (ImportStep03_NameSetMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep03_NameSetMappingViewModel && importVM.ProgressStep == "Name and set mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 3 should be active and progress label updated");
            step3.PrimaryActionButtonText.Should().Contain("Proceed");

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
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(8);
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuids).Should().Be(1); // One card should have multiple UUIDs due to multiple matches

            // =====================================================
            // Step 4 – Multiple UUID matches
            // =====================================================
            var step4 = (ImportStep04_MultipleUuidsViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep04_MultipleUuidsViewModel && importVM.ProgressStep == "Multiple versions found",
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
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(9);
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuids).Should().Be(0); // We should have resolved the multiple UUIDs, so none should have multiple anymore

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(7);
            var addtionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            addtionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            addtionalMappings[6].FieldToMap.Should().Be(ImportField.CardsForTrade);
            addtionalMappings[0].CsvHeaders.Should().HaveCount(18);

            // Assert CSV headers pre-selected
            addtionalMappings[0].SelectedCsvHeader.Should().Be("Condition"); // Condition
            addtionalMappings[1].SelectedCsvHeader.Should().Be("Printing"); // Finish
            addtionalMappings[2].SelectedCsvHeader.Should().Be("Language"); // Language
            addtionalMappings[3].SelectedCsvHeader.Should().Be(null); // Location
            addtionalMappings[4].SelectedCsvHeader.Should().Be("Note"); // Comment
            addtionalMappings[5].SelectedCsvHeader.Should().Be("Quantity"); // CardsOwned
            addtionalMappings[6].SelectedCsvHeader.Should().Be("For sale"); // CardsForTrade

            // Simulate clearing a mapping (should update the underlying ImportCardItem's AdditionalFieldsMapping for that field to null, but not affect the other fields)
            addtionalMappings[4].SelectedCsvHeader = null; // Clear Comment mapping

            // Proceed to next step
            var step5Result = await step5.OnPrimaryAction();

            // Assert step 5 completed successfully
            step5Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 6 - Conditions mapping
            // =====================================================
            var step6 = (ImportStep06_ConditionsMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep06_ConditionsMappingViewModel && importVM.ProgressStep == "Condition value mapping",
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
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep07_FinishMappingViewModel && importVM.ProgressStep == "Finish value mapping",
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
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep08_LanguageMappingViewModel && importVM.ProgressStep == "Language value mapping",
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

            // Assert step 8 completed successfully
            step8Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 10 - Summary and confirmation
            // =====================================================
            var step10 = (ImportStep10_SummaryViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep10_SummaryViewModel && importVM.ProgressStep == "Summary and confirmation",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 10 should be active and progress label updated");
            step10.PrimaryActionButtonText.Should().Contain("Start the import...");
            step10.CanExecuteSecondaryAction.Should().BeTrue();
            step10.SecondaryActionButtonText.Should().Contain("Save unrecognized items");

            var summary = step10.Summary;

            // Check totals
            summary.ReadyToImportCount.Should().Be(7); // 7 cards should be ready to import with UUIDs
            summary.TotalCardsToAdd.Should().Be(14); // Sum of quantities of all cards to import
            summary.UnableToImportCount.Should().Be(3); // 3 cards should not be able to import

            //// Check field mappings are correctly displayed in summary
            summary.FieldMappings[0].CsvHeader.Should().Be("Mapped to field: Condition");
            summary.FieldMappings[1].CsvHeader.Should().Be("Mapped to field: Printing");
            summary.FieldMappings[2].CsvHeader.Should().Be("Mapped to field: Language");
            summary.FieldMappings[5].CsvHeader.Should().Be("Mapped to field: Quantity");
            summary.FieldMappings[6].CsvHeader.Should().Be("Mapped to field: For sale");

            //// Spot check value mappings 
            summary.ValueMappings[0].Field.Should().Be(ImportField.Condition);
            summary.ValueMappings[0].CsvValue.Should().Be("Near Mint");
            summary.ValueMappings[0].MappedValue.Should().Be("Near Mint");
            summary.ValueMappings[1].Field.Should().Be(ImportField.Condition);
            summary.ValueMappings[1].CsvValue.Should().Be("Nearly sublime");
            summary.ValueMappings[1].MappedValue.Should().Be("(blank -> Near Mint)");
            summary.ValueMappings[7].Field.Should().Be(ImportField.CardFinish);
            summary.ValueMappings[7].CsvValue.Should().Be("Shiny");
            summary.ValueMappings[7].MappedValue.Should().Be("foil");
            summary.ValueMappings[8].Field.Should().Be(ImportField.CardFinish);
            summary.ValueMappings[8].CsvValue.Should().Be("nothing");
            summary.ValueMappings[8].MappedValue.Should().Be("(blank -> nonfoil)");
            summary.ValueMappings[12].CsvValue.Should().Be("Dansk");
            summary.ValueMappings[12].MappedValue.Should().Be("English");
            summary.ValueMappings[13].CsvValue.Should().Be("Spanish");
            summary.ValueMappings[13].MappedValue.Should().Be("Spanish");

            summary.UnimportableItems.Should().HaveCount(3);
            summary.UnimportableItems[0].CardName.Should().Contain("Does not exist");
            summary.UnimportableItems[1].Warnings.Should().Contain("Finish 'foil' is not available for UUID 0b952d69-5db0-59c2-810b-d4b10d452872.");
            summary.UnimportableItems[2].Warnings.Should().Contain("Language 'Spanish' is not available for UUID 7be5b8a9-0d68-5125-b729-ff1063dd3ed0.");

            // Proceed with the import
            var step10Result = await step10.OnPrimaryAction();

            // Assert that the final import completed successfully
            step10Result.Code.Should().Be(OperationResultCode.Success);

            var myCollectionInMemory = _mainVM.MyCollectionVM.Cards;
            myCollectionInMemory.Should().HaveCount(25);

            // Spotcheck individual cards
            var prismaticEndingUuid = "bafac74c-f4f8-5c71-8a6b-0bd02c536c47";
            var prismaticEnding = myCollectionInMemory.Single(c => c.Uuid == prismaticEndingUuid);
            prismaticEnding.Name.Should().Be("Prismatic Ending");
            prismaticEnding.SelectedCondition.Should().Be("Near Mint");
            prismaticEnding.SelectedFinish.Should().Be("nonfoil");
            prismaticEnding.Language.Should().Be("French");
            prismaticEnding.CardsOwned.Should().Be(7);
            prismaticEnding.CardsForTrade.Should().Be(3);

            var vexingArcanixUuid = "66dae17d-a742-51b4-ba09-0b37d7c64265";
            var vexingArcanix = myCollectionInMemory.Single(c => c.Uuid == vexingArcanixUuid);
            vexingArcanix.Name.Should().Be("Vexing Arcanix");
            vexingArcanix.SelectedCondition.Should().Be("Near Mint");
            vexingArcanix.SelectedFinish.Should().Be("nonfoil");
            vexingArcanix.Language.Should().Be("English");
            vexingArcanix.CardsOwned.Should().Be(2);
            vexingArcanix.CardsForTrade.Should().Be(0);

            var sokratesUuid = "3c389f9c-e459-5b16-87b5-d51644f05b25";
            var sokrates = myCollectionInMemory.Single(c => c.Uuid == sokratesUuid);
            sokrates.Name.Should().Be("Sokrates, Athenian Teacher");
            sokrates.SelectedCondition.Should().Be("Near Mint");
            sokrates.SelectedFinish.Should().Be("foil");
            sokrates.Language.Should().Be("Ancient Greek");
            sokrates.CardsOwned.Should().Be(2);
            sokrates.CardsForTrade.Should().Be(2);

            var syphonUuid = "9c015664-e6e8-53a4-ad48-276138b18098";
            var syphonSouls = myCollectionInMemory.Where(c => c.Uuid == syphonUuid).ToList();
            syphonSouls.Should().HaveCount(2);

            var nearMint = syphonSouls.Single(c => c.SelectedCondition == "Near Mint");
            nearMint.Name.Should().Be("Syphon Soul");
            nearMint.SelectedFinish.Should().Be("nonfoil");
            nearMint.Language.Should().Be("English");

            var mint = syphonSouls.Single(c => c.SelectedCondition == "Mint");
            mint.Name.Should().Be("Syphon Soul");
            mint.SelectedFinish.Should().Be("nonfoil");
            mint.Language.Should().Be("English");

            // Compare with database state to ensure it was correctly saved (spot check the same cards we checked in memory, and that the total count matches)
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            const string sql = @"
            SELECT uuid AS Uuids,
                   condition AS Conditions,
                   finish AS Finishes,
                   language AS Languages,
                   cardsOwned AS CardsOwned,
                   cardsForTrade AS CardsForTrade
            FROM myCollection;
            ";

            using var cmd = new SQLiteCommand(sql, uow.CurrentConnection);

            using var reader = await cmd.ExecuteReaderAsync();

            var myCollectionDB = new List<CardSet>();

            while (await reader.ReadAsync())
            {
                myCollectionDB.Add(new CardSet
                {
                    Uuid = reader.GetString(0),
                    SelectedCondition = reader.GetString(1),
                    SelectedFinish = reader.GetString(2),
                    Language = reader.GetString(3),
                    CardsOwned = reader.GetInt32(4),
                    CardsForTrade = reader.GetInt32(5)
                });
            }

            await uow.CommitAsync();

            myCollectionInMemory.Should().HaveCount(myCollectionDB.Count);

            var prismaticEndingDb = myCollectionDB.Single(c =>
                c.Uuid == prismaticEnding.Uuid &&
                c.SelectedCondition == prismaticEnding.SelectedCondition &&
                c.SelectedFinish == prismaticEnding.SelectedFinish &&
                c.Language == prismaticEnding.Language &&
                c.SelectedLocationId == prismaticEnding.SelectedLocationId &&
                c.Comment == prismaticEnding.Comment);
            prismaticEndingDb.CardsOwned.Should().Be(prismaticEnding.CardsOwned);
            prismaticEndingDb.CardsForTrade.Should().Be(prismaticEnding.CardsForTrade);

            var vexingArcanixDb = myCollectionDB.Single(c =>
                c.Uuid == vexingArcanix.Uuid &&
                c.SelectedCondition == vexingArcanix.SelectedCondition &&
                c.SelectedFinish == vexingArcanix.SelectedFinish &&
                c.Language == vexingArcanix.Language &&
                c.SelectedLocationId == vexingArcanix.SelectedLocationId &&
                c.Comment == vexingArcanix.Comment);
            vexingArcanixDb.CardsOwned.Should().Be(vexingArcanix.CardsOwned);
            vexingArcanixDb.CardsForTrade.Should().Be(vexingArcanix.CardsForTrade);

            var sokratesDb = myCollectionDB.Single(c =>
                c.Uuid == sokrates.Uuid &&
                c.SelectedCondition == sokrates.SelectedCondition &&
                c.SelectedFinish == sokrates.SelectedFinish &&
                c.Language == sokrates.Language &&
                c.SelectedLocationId == sokrates.SelectedLocationId &&
                c.Comment == sokrates.Comment);
            sokratesDb.CardsOwned.Should().Be(sokrates.CardsOwned);
            sokratesDb.CardsForTrade.Should().Be(sokrates.CardsForTrade);

            var syphonNearMintDb = myCollectionDB.Single(c =>
                c.Uuid == nearMint.Uuid &&
                c.SelectedCondition == nearMint.SelectedCondition &&
                c.SelectedFinish == nearMint.SelectedFinish &&
                c.Language == nearMint.Language &&
                c.SelectedLocationId == nearMint.SelectedLocationId &&
                c.Comment == nearMint.Comment);
            syphonNearMintDb.CardsOwned.Should().Be(nearMint.CardsOwned);
            syphonNearMintDb.CardsForTrade.Should().Be(nearMint.CardsForTrade);

            var syphonMintDb = myCollectionDB.Single(c =>
                c.Uuid == mint.Uuid &&
                c.SelectedCondition == mint.SelectedCondition &&
                c.SelectedFinish == mint.SelectedFinish &&
                c.Language == mint.Language &&
                c.SelectedLocationId == mint.SelectedLocationId &&
                c.Comment == mint.Comment);
            syphonMintDb.CardsOwned.Should().Be(mint.CardsOwned);
            syphonMintDb.CardsForTrade.Should().Be(mint.CardsForTrade);

            // =====================================================
            // Step 11 - Final
            // =====================================================
            var step11 = (ImportStep11_FinishViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep11_FinishViewModel && importVM.ProgressStep == "",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 11 should be active and progress label updated");
            step11.PrimaryActionButtonText.Should().Contain("OK");
        }

        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }

    }
    #endregion

    #region Import Scenario 2
    public sealed class ImportScenarioTests2(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        readonly static string csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest2.csv");

        private readonly InMemoryDatabaseFixture _fx = fx;
        private IDbConnectionFactory _dbFactory = null!;
        private readonly TestPromptService _prompt = new();
        private readonly TestFileSystemPicker _picker = new(csvPath);
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: _prompt, filePickerOverride: _picker);
        }

        [Fact]
        public async Task Import_scenario_2()
        {
            File.Exists(csvPath).Should().BeTrue();

            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().HaveCount(22);

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep01_StartViewModel && importVM.ProgressHeadline == "The Import Wizard",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 1 should be active and progress label updated");
            step1.PrimaryActionButtonText.Should().Contain("Let's go");

            var step1Result = await step1.OnPrimaryAction(); // Parse CSV

            // Assert step 1 completed successfully
            step1Result.Code.Should().Be(OperationResultCode.Success);
            importVM.ImportCardList.Should().HaveCount(6);

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

            mapping.CsvHeaders.Should().HaveCount(11);
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

            // Map to Scryfall Id
            mapping.SelectedCsvHeader = "ScryFallId";
            mapping.SelectedDatabaseField = "scryfallId";

            // CanExecute should now be true
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Proceed to map using Id
            var step2Result = await step2.OnPrimaryAction();

            // Assert step 2 completed successfully
            step2Result.Code.Should().Be(OperationResultCode.Success);

            // After ID mapping, we should have 4 cards with UUIDs 
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(4);

            // =====================================================
            // Step 3 – Name & set mapping
            // =====================================================
            var step3 = (ImportStep03_NameSetMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep03_NameSetMappingViewModel && importVM.ProgressStep == "Name and set mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 3 should be active and progress label updated");
            step3.PrimaryActionButtonText.Should().Contain("Proceed");

            // Cancel the import to test that cancel works at this step (and doesn't cause any issues if we restart the import afterwards)
            importVM.CancelCommand.Execute(null);

            var step10AfterCancel = (ImportStep11_FinishViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep11_FinishViewModel && importVM.ProgressStep == "",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 11 should be active and progress label updated");

            importVM.ProgressHeadline.Should().Be("Import cancelled");
            importVM.ProgressDetailMessage.Should().Contain("User cancellation - no cards imported to collection.");

            step10AfterCancel.PrimaryActionButtonText.Should().Contain("OK");

            var step10AfterCancelResult = await step10AfterCancel.OnPrimaryAction();
            step10AfterCancelResult.Code.Should().Be(OperationResultCode.Success);

            // Restart the import after cancellation and continue up to step 3 again

            // Step 1
            importVM.Begin();
            var step1AfterRestart = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;
            var step1AfterRestartResult = await step1.OnPrimaryAction();
            step1AfterRestartResult.Code.Should().Be(OperationResultCode.Success);

            // Step 2
            var step2AfterRestart = (ImportStep02_IdMappingViewModel)importVM.CurrentStepViewModel;
            var mappingAfterRestart = step2AfterRestart.IdMappings[0];
            mappingAfterRestart.SelectedCsvHeader = "ScryFallId";
            mappingAfterRestart.SelectedDatabaseField = "scryfallId";
            var step2AfterRestartResult = await step2.OnPrimaryAction();
            step2AfterRestartResult.Code.Should().Be(OperationResultCode.Success);

            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(4);

            // Step 3
            var step3AfterRestart = (ImportStep03_NameSetMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep03_NameSetMappingViewModel && importVM.ProgressStep == "Name and set mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 3 should be active and progress label updated");

            step3AfterRestart.NameSetMappings.Should().HaveCount(3);
            var nameSetmapping = step3AfterRestart.NameSetMappings;

            // Assert that no CSV headers are pre-selected
            nameSetmapping[0].SelectedCsvHeader.Should().Be(null);
            nameSetmapping[1].SelectedCsvHeader.Should().Be(null);
            nameSetmapping[2].SelectedCsvHeader.Should().Be(null);

            // Choose a value for card name mapping
            step3AfterRestart.CanExecutePrimaryAction.Should().BeFalse();
            nameSetmapping[0].SelectedCsvHeader = "Kortnavn";
            step3AfterRestart.CanExecutePrimaryAction.Should().BeTrue();

            var step3AfterRestartResult = await step3AfterRestart.OnPrimaryAction();

            // Assert step 3 completed successfully
            step3AfterRestartResult.Code.Should().Be(OperationResultCode.Success);

            // No change after name and set mapping
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(5);

            // There are no items with multiple UUIDs so we skip step 4
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuids).Should().Be(0);

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(7);
            var addtionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            addtionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            addtionalMappings[6].FieldToMap.Should().Be(ImportField.CardsForTrade);
            addtionalMappings[0].CsvHeaders.Should().HaveCount(11);

            // Assert CSV headers pre-selected
            addtionalMappings[0].SelectedCsvHeader.Should().Be(null);
            addtionalMappings[1].SelectedCsvHeader.Should().Be("Finish");
            addtionalMappings[2].SelectedCsvHeader.Should().Be(null);
            addtionalMappings[3].SelectedCsvHeader.Should().Be(null);
            addtionalMappings[4].SelectedCsvHeader.Should().Be(null);
            addtionalMappings[5].SelectedCsvHeader.Should().Be(null);
            addtionalMappings[6].SelectedCsvHeader.Should().Be(null);

            // Select a value for cards for trade
            addtionalMappings[6].SelectedCsvHeader = "TilSalg";

            // Clear CardFinish mapping so we use defaults for everything except cards for trade
            addtionalMappings[1].SelectedCsvHeader = null;

            // Proceed to summary step
            var step5Result = await step5.OnPrimaryAction();

            // Assert step 5 completed successfully
            step5Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 10 - Summary and confirmation
            // =====================================================
            var step10 = (ImportStep10_SummaryViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep10_SummaryViewModel && importVM.ProgressStep == "Summary and confirmation",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 10 should be active and progress label updated");
            step10.PrimaryActionButtonText.Should().Contain("Start the import...");
            step10.CanExecuteSecondaryAction.Should().BeTrue();
            step10.SecondaryActionButtonText.Should().Contain("Save unrecognized items");

            var summary = step10.Summary;

            // Check totals
            summary.ReadyToImportCount.Should().Be(5); // 5 cards should be ready to import with UUIDs
            summary.TotalCardsToAdd.Should().Be(5); // Sum of quantities of all cards to import
            summary.UnableToImportCount.Should().Be(1); // 1 card should not be able to import

            // Check field mappings are correctly displayed in summary
            summary.FieldMappings[0].CsvHeader.Should().Be("No value chosen for field Condition - using default value: \"Near Mint\" for all imports");
            summary.FieldMappings[1].CsvHeader.Should().Be("No value chosen for field CardFinish - using default value: \"nonfoil\" for all imports");
            summary.FieldMappings[2].CsvHeader.Should().Be("No value chosen for field Language - using default value: \"English\" for all imports");
            summary.FieldMappings[3].CsvHeader.Should().Be("No value chosen for field Location - using default value: \"blank\" for all imports"); ;
            summary.FieldMappings[4].CsvHeader.Should().Be("No value chosen for field Comment - using default value: \"blank\" for all imports"); ;
            summary.FieldMappings[5].CsvHeader.Should().Be("No value chosen for field CardsOwned - using default value: \"1\" for all imports"); ;
            summary.FieldMappings[6].CsvHeader.Should().Be("Mapped to field: TilSalg");

            // Check value mappings 
            summary.ValueMappings[0].Field.Should().Be(ImportField.None);
            summary.ValueMappings[0].CsvValue.Should().Be("—");
            summary.ValueMappings[0].MappedValue.Should().Be("All values use defaults");

            summary.UnimportableItems.Should().HaveCount(1);
            summary.UnimportableItems[0].CardName.Should().Contain("Brisela, Voice of Nightmares");
            summary.UnimportableItems[0].Warnings.Should().Contain("No UUID resolved for this row (cannot import). Check ID / Name+Set mapping steps.");

            // Proceed with the import
            var step10Result = await step10.OnPrimaryAction();

            // Assert that the final import completed successfully
            step10Result.Code.Should().Be(OperationResultCode.Success);

            var myCollectionInMemory = _mainVM.MyCollectionVM.Cards;
            myCollectionInMemory.Should().HaveCount(26);

            //// Spotcheck individual cards
            var chillarpillarUuid = "d4588e8f-e5a0-53e5-ac90-0a5183f0d118";
            var chillarpillar = myCollectionInMemory.Single(c => c.Uuid == chillarpillarUuid && c.SelectedCondition == "Near Mint");

            chillarpillar.Name.Should().Be("Chillerpillar // Chillerpillar");
            chillarpillar.SelectedFinish.Should().Be("nonfoil");
            chillarpillar.Language.Should().Be("English");
            chillarpillar.CardsOwned.Should().Be(2);
            chillarpillar.CardsForTrade.Should().Be(2);

            var realmwalkerUuid = "66124810-2a79-5c4f-a43f-181400aa8c4f";
            var realmwalker = myCollectionInMemory.Single(c => c.Uuid == realmwalkerUuid);
            realmwalker.Name.Should().Be("Realmwalker");
            realmwalker.SelectedCondition.Should().Be("Near Mint");
            realmwalker.SelectedFinish.Should().Be("foil");
            realmwalker.Language.Should().Be("English");
            realmwalker.CardsOwned.Should().Be(1);
            realmwalker.CardsForTrade.Should().Be(1);

            var zombieUuid = "011a9246-7f7c-50c7-ab99-3fc13469c13b";
            var zombie = myCollectionInMemory.Single(c => c.Uuid == zombieUuid);
            zombie.Name.Should().Be("Zombie");
            zombie.SelectedCondition.Should().Be("Near Mint");
            zombie.SelectedFinish.Should().Be("nonfoil");
            zombie.Language.Should().Be("English");
            zombie.CardsOwned.Should().Be(1); // defaults to 1 because CardsOwned mapping was not set
            zombie.CardsForTrade.Should().Be(0);

            var neverReturnUuid = "875ba98c-721c-537b-b326-22d803fab7c0"; // uuid of the 'a' side
            var neverReturn = myCollectionInMemory.Single(c => c.Uuid == neverReturnUuid);
            neverReturn.Name.Should().Be("Never // Return");
            neverReturn.SelectedCondition.Should().Be("Near Mint");
            neverReturn.SelectedFinish.Should().Be("nonfoil");
            neverReturn.Language.Should().Be("English");
            neverReturn.CardsOwned.Should().Be(1);
            neverReturn.CardsForTrade.Should().Be(0);

            // Compare with database state to ensure it was correctly saved (spot check the same cards we checked in memory, and that the total count matches)
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            const string sql = @"
            SELECT uuid AS Uuids,
                   condition AS Conditions,
                   finish AS Finishes,
                   language AS Languages,
                   cardsOwned AS CardsOwned,
                   cardsForTrade AS CardsForTrade
            FROM myCollection;
            ";

            using var cmd = new SQLiteCommand(sql, uow.CurrentConnection);
            using var reader = await cmd.ExecuteReaderAsync();
            var myCollectionDB = new List<CardSet>();

            while (await reader.ReadAsync())
            {
                myCollectionDB.Add(new CardSet
                {
                    Uuid = reader.GetString(0),
                    SelectedCondition = reader.GetString(1),
                    SelectedFinish = reader.GetString(2),
                    Language = reader.GetString(3),
                    CardsOwned = reader.GetInt32(4),
                    CardsForTrade = reader.GetInt32(5)
                });
            }

            await uow.CommitAsync();

            myCollectionInMemory.Should().HaveCount(myCollectionDB.Count);

            var chillarpillarDb = myCollectionDB.Single(c =>
                c.Uuid == chillarpillar.Uuid &&
                c.SelectedCondition == chillarpillar.SelectedCondition &&
                c.SelectedFinish == chillarpillar.SelectedFinish &&
                c.Language == chillarpillar.Language &&
                c.SelectedLocationId == chillarpillar.SelectedLocationId &&
                c.Comment == chillarpillar.Comment);
            chillarpillarDb.CardsOwned.Should().Be(chillarpillar.CardsOwned);
            chillarpillarDb.CardsForTrade.Should().Be(chillarpillar.CardsForTrade);

            var realmWalkerDb = myCollectionDB.Single(c =>
                c.Uuid == realmwalker.Uuid &&
                c.SelectedCondition == realmwalker.SelectedCondition &&
                c.SelectedFinish == realmwalker.SelectedFinish &&
                c.Language == realmwalker.Language &&
                c.SelectedLocationId == realmwalker.SelectedLocationId &&
                c.Comment == realmwalker.Comment);
            realmWalkerDb.CardsOwned.Should().Be(realmwalker.CardsOwned);
            realmWalkerDb.CardsForTrade.Should().Be(realmwalker.CardsForTrade);

            var zombieDb = myCollectionDB.Single(c =>
                c.Uuid == zombie.Uuid &&
                c.SelectedCondition == zombie.SelectedCondition &&
                c.SelectedFinish == zombie.SelectedFinish &&
                c.Language == zombie.Language &&
                c.SelectedLocationId == zombie.SelectedLocationId &&
                c.Comment == zombie.Comment);
            zombieDb.CardsOwned.Should().Be(zombie.CardsOwned);
            zombieDb.CardsForTrade.Should().Be(zombie.CardsForTrade);

            var neverReturnDb = myCollectionDB.Single(c =>
                c.Uuid == neverReturn.Uuid &&
                c.SelectedCondition == neverReturn.SelectedCondition &&
                c.SelectedFinish == neverReturn.SelectedFinish &&
                c.Language == neverReturn.Language &&
                c.SelectedLocationId == neverReturn.SelectedLocationId &&
                c.Comment == neverReturn.Comment);
            neverReturnDb.CardsOwned.Should().Be(neverReturn.CardsOwned);
            neverReturnDb.CardsForTrade.Should().Be(neverReturn.CardsForTrade);

            // =====================================================
            // Step 11 - Final
            // =====================================================
            var step11 = (ImportStep11_FinishViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep11_FinishViewModel && importVM.ProgressStep == "",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 11 should be active and progress label updated");
            step11.PrimaryActionButtonText.Should().Contain("OK");
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Import Scenario 3
    public sealed class ImportScenarioTests3(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        readonly static string csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest3.csv");

        private readonly InMemoryDatabaseFixture _fx = fx;
        private IDbConnectionFactory _dbFactory = null!;
        private readonly TestPromptService _prompt = new();
        private readonly TestFileSystemPicker _picker = new(csvPath);
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: _prompt, filePickerOverride: _picker);
        }

        [Fact]
        public async Task Import_scenario_3()
        {
            File.Exists(csvPath).Should().BeTrue();

            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().HaveCount(22);

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep01_StartViewModel && importVM.ProgressHeadline == "The Import Wizard",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 1 should be active and progress label updated");
            step1.PrimaryActionButtonText.Should().Contain("Let's go");

            var step1Result = await step1.OnPrimaryAction(); // Parse CSV

            // Assert step 1 completed successfully
            step1Result.Code.Should().Be(OperationResultCode.Success);
            importVM.ImportCardList.Should().HaveCount(4);

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

            mapping.CsvHeaders.Should().HaveCount(3);

            // Map to UUID Id
            mapping.SelectedCsvHeader = "UUID";
            mapping.SelectedDatabaseField = "uuid";

            // CanExecute should now be true
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Proceed to map using Id
            var step2Result = await step2.OnPrimaryAction();

            // Assert step 2 completed successfully
            step2Result.Code.Should().Be(OperationResultCode.Success);

            // After ID mapping, we should have 4 cards with UUIDs 
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(4);

            // There are no items with multiple UUIDs so we skip directly to step 5
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuids).Should().Be(0);

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(7);
            var additionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            additionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            additionalMappings[6].FieldToMap.Should().Be(ImportField.CardsForTrade);
            additionalMappings[0].CsvHeaders.Should().HaveCount(3);

            // Assert CSV headers pre-selected
            additionalMappings[0].SelectedCsvHeader.Should().Be(null);
            additionalMappings[1].SelectedCsvHeader.Should().Be(null);
            additionalMappings[2].SelectedCsvHeader.Should().Be(null);
            additionalMappings[3].SelectedCsvHeader.Should().Be("Location");
            additionalMappings[4].SelectedCsvHeader.Should().Be(null);
            additionalMappings[5].SelectedCsvHeader.Should().Be(null);
            additionalMappings[6].SelectedCsvHeader.Should().Be(null);

            // Proceed to location mapping step
            var step5Result = await step5.OnPrimaryAction();

            // Assert step 5 completed successfully
            step5Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 9 - Location mapping
            // =====================================================
            var step9 = (ImportStep09_LocationMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep09_LocationMappingViewModel && importVM.ProgressStep == "Location value mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 9 should be active and progress label updated");
            step9.PrimaryActionButtonText.Should().Contain("Proceed");

            step9.LocationMappings.Should().HaveCount(4);
            var locationMappings = step9.LocationMappings;

            // Check LocationMappingItem object is correctly initialized with guesses
            locationMappings[0].CsvValue.Should().Be("Binder 1");
            locationMappings[0].SelectedCardSetValue.Should().Be("Binder 1");
            locationMappings[1].CsvValue.Should().Be("Binder 2");
            locationMappings[1].SelectedCardSetValue.Should().Be(null);
            locationMappings[2].CsvValue.Should().Be("Aggro Fish");
            locationMappings[2].SelectedCardSetValue.Should().Be("Aggro Fish");
            locationMappings[3].CsvValue.Should().Be("Flameout Fortune");
            locationMappings[3].SelectedCardSetValue.Should().Be(null);

            // Proceed to next step
            var step9Result = await step9.OnPrimaryAction();

            // Assert step 9 completed successfully
            step9Result.Code.Should().Be(OperationResultCode.Success);


            // =====================================================
            // Step 10 - Summary and confirmation
            // =====================================================
            var step10 = (ImportStep10_SummaryViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep10_SummaryViewModel && importVM.ProgressStep == "Summary and confirmation",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 10 should be active and progress label updated");
            step10.PrimaryActionButtonText.Should().Contain("Start the import...");
            step10.IsSecondaryActionVisible.Should().BeFalse();

            var summary = step10.Summary;

            // Check totals
            summary.ReadyToImportCount.Should().Be(4); // 5 cards should be ready to import with UUIDs
            summary.TotalCardsToAdd.Should().Be(4); // Sum of quantities of all cards to import
            summary.UnableToImportCount.Should().Be(0); // 0 card should not be able to import

            // Check field mappings are correctly displayed in summary
            summary.FieldMappings[0].CsvHeader.Should().Be("No value chosen for field Condition - using default value: \"Near Mint\" for all imports");
            summary.FieldMappings[1].CsvHeader.Should().Be("No value chosen for field CardFinish - using default value: \"nonfoil\" for all imports");
            summary.FieldMappings[2].CsvHeader.Should().Be("No value chosen for field Language - using default value: \"English\" for all imports");
            summary.FieldMappings[3].CsvHeader.Should().Be("Mapped to field: Location");
            summary.FieldMappings[4].CsvHeader.Should().Be("No value chosen for field Comment - using default value: \"blank\" for all imports");
            summary.FieldMappings[5].CsvHeader.Should().Be("No value chosen for field CardsOwned - using default value: \"1\" for all imports");
            summary.FieldMappings[6].CsvHeader.Should().Be("No value chosen for field CardsForTrade - using default value: \"0\" for all imports");

            // Check value mappings 
            summary.ValueMappings[0].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[0].CsvValue.Should().Be("Binder 1");
            summary.ValueMappings[0].MappedValue.Should().Be("Binder 1");
            summary.ValueMappings[1].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[1].CsvValue.Should().Be("Binder 2");
            summary.ValueMappings[1].MappedValue.Should().Be("(blank -> blank)");
            summary.ValueMappings[2].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[2].CsvValue.Should().Be("Aggro Fish");
            summary.ValueMappings[2].MappedValue.Should().Be("Aggro Fish");
            summary.ValueMappings[3].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[3].CsvValue.Should().Be("Flameout Fortune");
            summary.ValueMappings[3].MappedValue.Should().Be("(blank -> blank)");


            // Proceed with the import
            var step10Result = await step10.OnPrimaryAction();

            // Assert that the final import completed successfully
            step10Result.Code.Should().Be(OperationResultCode.Success);

            var myCollectionInMemory = _mainVM.MyCollectionVM.Cards;
            myCollectionInMemory.Should().HaveCount(25);

            //// Spotcheck individual cards
            var angelOfGlorysRiseUuid = "aff7557c-2e85-5bda-8231-d8f1e46b43c8";

            // Angel collection identity 1 - should be imported with location mapped to "Binder 1" and other fields using defaults
            var angel1 = myCollectionInMemory.Single(c => c.Uuid == angelOfGlorysRiseUuid && c.SelectedLocationDisplayName == "Storage: Binder 1");
            angel1.Name.Should().Be("Angel of Glory's Rise");
            angel1.SelectedFinish.Should().Be("foil");
            angel1.Language.Should().Be("English");
            angel1.CardsOwned.Should().Be(1);
            angel1.CardsForTrade.Should().Be(0);

            // Angel collection identity 2 - should be imported with blank location and other fields using defaults
            var angel2 = myCollectionInMemory.Single(c => c.Uuid == angelOfGlorysRiseUuid && c.SelectedLocationDisplayName == null);
            angel2.Name.Should().Be("Angel of Glory's Rise");
            angel2.SelectedFinish.Should().Be("foil");
            angel2.Language.Should().Be("English");
            angel2.CardsOwned.Should().Be(2);
            angel2.CardsForTrade.Should().Be(0);

            // Angel collection identity 3 - should be imported with location mapped to "Aggro Fish" and other fields using defaults
            var angel3 = myCollectionInMemory.Single(c => c.Uuid == angelOfGlorysRiseUuid && c.SelectedLocationDisplayName == "Deck: Aggro Fish");
            angel3.Name.Should().Be("Angel of Glory's Rise");
            angel3.SelectedFinish.Should().Be("foil");
            angel3.Language.Should().Be("English");
            angel3.CardsOwned.Should().Be(1);
            angel3.CardsForTrade.Should().Be(0);

            // Compare with database state to ensure it was correctly saved (spot check the same cards we checked in memory, and that the total count matches)
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            const string sql = @"
            SELECT uuid AS Uuids,
                   condition AS Conditions,
                   finish AS Finishes,
                   language AS Languages,
                   locationId AS LocationIds,
                   cardsOwned AS CardsOwned,
                   cardsForTrade AS CardsForTrade
            FROM myCollection;
            ";

            using var cmd = new SQLiteCommand(sql, uow.CurrentConnection);
            using var reader = await cmd.ExecuteReaderAsync();
            var myCollectionDB = new List<CardSet>();

            while (await reader.ReadAsync())
            {
                myCollectionDB.Add(new CardSet
                {
                    Uuid = reader.GetString(0),
                    SelectedCondition = reader.GetString(1),
                    SelectedFinish = reader.GetString(2),
                    Language = reader.GetString(3),
                    SelectedLocationId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CardsOwned = reader.GetInt32(5),
                    CardsForTrade = reader.GetInt32(6)
                });
            }

            await uow.CommitAsync();

            myCollectionInMemory.Should().HaveCount(myCollectionDB.Count);

            var angel1Db = myCollectionDB.Single(c =>
                c.Uuid == angel1.Uuid &&
                c.SelectedCondition == angel1.SelectedCondition &&
                c.SelectedFinish == angel1.SelectedFinish &&
                c.Language == angel1.Language &&
                c.SelectedLocationId == angel1.SelectedLocationId &&
                c.Comment == angel1.Comment);
            angel1Db.CardsOwned.Should().Be(angel1.CardsOwned);
            angel1Db.CardsForTrade.Should().Be(angel1.CardsForTrade);

            var angel2Db = myCollectionDB.Single(c =>
                c.Uuid == angel2.Uuid &&
                c.SelectedCondition == angel2.SelectedCondition &&
                c.SelectedFinish == angel2.SelectedFinish &&
                c.Language == angel2.Language &&
                c.SelectedLocationId == angel2.SelectedLocationId &&
                c.Comment == angel2.Comment);
            angel2Db.CardsOwned.Should().Be(angel2.CardsOwned);
            angel2Db.CardsForTrade.Should().Be(angel2.CardsForTrade);

            var angel3Db = myCollectionDB.Single(c =>
                c.Uuid == angel3.Uuid &&
                c.SelectedCondition == angel3.SelectedCondition &&
                c.SelectedFinish == angel3.SelectedFinish &&
                c.Language == angel3.Language &&
                c.SelectedLocationId == angel3.SelectedLocationId &&
                c.Comment == angel3.Comment);
            angel3Db.CardsOwned.Should().Be(angel3.CardsOwned);
            angel3Db.CardsForTrade.Should().Be(angel3.CardsForTrade);

            // =====================================================
            // Step 11 - Final
            // =====================================================
            var step11 = (ImportStep11_FinishViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep11_FinishViewModel && importVM.ProgressStep == "",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 11 should be active and progress label updated");
            step11.PrimaryActionButtonText.Should().Contain("OK");
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Import Scenario 4
    public sealed class ImportScenarioTests4(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        readonly static string csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest4.csv");

        private readonly InMemoryDatabaseFixture _fx = fx;
        private IDbConnectionFactory _dbFactory = null!;
        private readonly TestPromptService _prompt = new();
        private readonly TestFileSystemPicker _picker = new(csvPath);
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: _prompt, filePickerOverride: _picker);
        }

        [Fact]
        public async Task Import_scenario_4()
        {
            File.Exists(csvPath).Should().BeTrue();

            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().HaveCount(22);

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep01_StartViewModel && importVM.ProgressHeadline == "The Import Wizard",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 1 should be active and progress label updated");
            step1.PrimaryActionButtonText.Should().Contain("Let's go");

            var step1Result = await step1.OnPrimaryAction(); // Parse CSV

            // Assert step 1 completed successfully
            step1Result.Code.Should().Be(OperationResultCode.Success);
            importVM.ImportCardList.Should().HaveCount(5);

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

            mapping.CsvHeaders.Should().HaveCount(4);

            // Map to UUID Id
            mapping.SelectedCsvHeader = "UUID";
            mapping.SelectedDatabaseField = "uuid";

            // CanExecute should now be true
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Proceed to map using Id
            var step2Result = await step2.OnPrimaryAction();

            // Assert step 2 completed successfully
            step2Result.Code.Should().Be(OperationResultCode.Success);

            // After ID mapping, we should have 4 cards with UUIDs 
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuid).Should().Be(5);

            // There are no items with multiple UUIDs so we skip directly to step 5
            importVM.ImportCardList.Count(ImportScenarioTestsHelpers.HasUuids).Should().Be(0);

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(7);
            var additionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            additionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            additionalMappings[6].FieldToMap.Should().Be(ImportField.CardsForTrade);
            additionalMappings[0].CsvHeaders.Should().HaveCount(4);

            // Assert CSV headers pre-selected
            additionalMappings[0].SelectedCsvHeader.Should().Be(null);
            additionalMappings[1].SelectedCsvHeader.Should().Be(null);
            additionalMappings[2].SelectedCsvHeader.Should().Be(null);
            additionalMappings[3].SelectedCsvHeader.Should().Be(null);
            additionalMappings[4].SelectedCsvHeader.Should().Be("Comment");
            additionalMappings[5].SelectedCsvHeader.Should().Be(null);
            additionalMappings[6].SelectedCsvHeader.Should().Be(null);

            // Map Location to "Sted" column
            additionalMappings[3].SelectedCsvHeader = "Sted";

            // Proceed to location mapping step
            var step5Result = await step5.OnPrimaryAction();

            // Assert step 5 completed successfully
            step5Result.Code.Should().Be(OperationResultCode.Success);

            // =====================================================
            // Step 9 - Location mapping
            // =====================================================
            var step9 = (ImportStep09_LocationMappingViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep09_LocationMappingViewModel && importVM.ProgressStep == "Location value mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 9 should be active and progress label updated");
            step9.PrimaryActionButtonText.Should().Contain("Proceed");

            step9.LocationMappings.Should().HaveCount(4);
            var locationMappings = step9.LocationMappings;

            // Check LocationMappingItem object is correctly initialized with guesses
            locationMappings[0].CsvValue.Should().Be("Binder 1");
            locationMappings[0].SelectedCardSetValue.Should().Be("Binder 1");
            locationMappings[1].CsvValue.Should().Be("Binder 2");
            locationMappings[1].SelectedCardSetValue.Should().Be(null);
            locationMappings[2].CsvValue.Should().Be("Aggro Fish");
            locationMappings[2].SelectedCardSetValue.Should().Be("Aggro Fish");
            locationMappings[3].CsvValue.Should().Be("Binder 3");
            locationMappings[3].SelectedCardSetValue.Should().Be(null);

            // Change mapping for "Aggro Fish" to be blank
            locationMappings[2].SelectedCardSetValue = null;

            // Proceed to next step, but choose to create missing locations
            var step9Result = await step9.OnSecondaryAction();

            // Assert step 9 completed successfully
            step9Result.Code.Should().Be(OperationResultCode.Success);


            // =====================================================
            // Step 10 - Summary and confirmation
            // =====================================================
            var step10 = (ImportStep10_SummaryViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep10_SummaryViewModel && importVM.ProgressStep == "Summary and confirmation",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 10 should be active and progress label updated");
            step10.PrimaryActionButtonText.Should().Contain("Start the import...");
            step10.IsSecondaryActionVisible.Should().BeFalse();

            var summary = step10.Summary;

            // Check totals
            summary.ReadyToImportCount.Should().Be(5); // 5 cards should be ready to import with UUIDs
            summary.TotalCardsToAdd.Should().Be(5); // Sum of quantities of all cards to import
            summary.UnableToImportCount.Should().Be(0); // All cards should be able to import

            // Check field mappings are correctly displayed in summary
            summary.FieldMappings[0].CsvHeader.Should().Be("No value chosen for field Condition - using default value: \"Near Mint\" for all imports");
            summary.FieldMappings[1].CsvHeader.Should().Be("No value chosen for field CardFinish - using default value: \"nonfoil\" for all imports");
            summary.FieldMappings[2].CsvHeader.Should().Be("No value chosen for field Language - using default value: \"English\" for all imports");
            summary.FieldMappings[3].CsvHeader.Should().Be("Mapped to field: Sted");
            summary.FieldMappings[4].CsvHeader.Should().Be("Mapped to field: Comment");
            summary.FieldMappings[5].CsvHeader.Should().Be("No value chosen for field CardsOwned - using default value: \"1\" for all imports");
            summary.FieldMappings[6].CsvHeader.Should().Be("No value chosen for field CardsForTrade - using default value: \"0\" for all imports");

            // Check value mappings 
            summary.ValueMappings[0].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[0].CsvValue.Should().Be("Binder 1");
            summary.ValueMappings[0].MappedValue.Should().Be("Binder 1");
            summary.ValueMappings[1].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[1].CsvValue.Should().Be("Binder 2");
            summary.ValueMappings[1].MappedValue.Should().Be("Binder 2");
            summary.ValueMappings[2].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[2].CsvValue.Should().Be("Aggro Fish");
            summary.ValueMappings[2].MappedValue.Should().Be("Aggro Fish");
            summary.ValueMappings[3].Field.Should().Be(ImportField.Location);
            summary.ValueMappings[3].CsvValue.Should().Be("Binder 3");
            summary.ValueMappings[3].MappedValue.Should().Be("Binder 3");

            // Proceed with the import
            var step10Result = await step10.OnPrimaryAction();

            // Assert that the final import completed successfully
            step10Result.Code.Should().Be(OperationResultCode.Success);

            var myCollectionInMemory = _mainVM.MyCollectionVM.Cards;
            myCollectionInMemory.Should().HaveCount(27);

            // Spotcheck individual cards
            var angelOfGlorysRiseUuid = "aff7557c-2e85-5bda-8231-d8f1e46b43c8";
            var snappingSailbackUuid = "154a09f3-65e3-5821-bc02-bd972b3be676";

            // Angel collection identity 1 - should be imported with location mapped to "Binder 1", comment and other fields using defaults
            var angel1 = myCollectionInMemory.Single(c => c.Uuid == angelOfGlorysRiseUuid && c.SelectedLocationDisplayName == "Storage: Binder 1");
            angel1.Name.Should().Be("Angel of Glory's Rise");
            angel1.SelectedFinish.Should().Be("foil");
            angel1.Language.Should().Be("English");
            angel1.Comment.Should().Be("water damage");
            angel1.CardsOwned.Should().Be(1);
            angel1.CardsForTrade.Should().Be(0);

            // Angel collection identity 2 - should be imported with newly created location, comment and other fields using defaults
            var angel2 = myCollectionInMemory.Single(c => c.Uuid == angelOfGlorysRiseUuid && c.SelectedLocationDisplayName == "Storage: Binder 2");
            angel2.Name.Should().Be("Angel of Glory's Rise");
            angel2.SelectedFinish.Should().Be("foil");
            angel2.Language.Should().Be("English");
            angel2.Comment.Should().Be("pen mark");
            angel2.CardsOwned.Should().Be(1);
            angel2.CardsForTrade.Should().Be(0);

            // Sailback collection identity 3 - should be imported with location mapped to "aggro fish", blank comment and other fields using defaults
            var sailback3 = myCollectionInMemory.Single(c => c.Uuid == snappingSailbackUuid && c.SelectedLocationDisplayName == "Deck: Aggro Fish");
            sailback3.Name.Should().Be("Snapping Sailback");
            sailback3.SelectedFinish.Should().Be("nonfoil");
            sailback3.Language.Should().Be("English");
            sailback3.Comment.Should().Be(null);
            sailback3.CardsOwned.Should().Be(1);
            sailback3.CardsForTrade.Should().Be(0);

            // Sailback collection identity 4 - should be imported with location mapped to "Binder 3", blank comment and other fields using defaults
            var sailback4 = myCollectionInMemory.Single(c => c.Uuid == snappingSailbackUuid && c.SelectedLocationDisplayName == "Storage: Binder 3");
            sailback4.Name.Should().Be("Snapping Sailback");
            sailback4.SelectedFinish.Should().Be("nonfoil");
            sailback4.Language.Should().Be("English");
            sailback4.Comment.Should().Be(null);
            sailback4.CardsOwned.Should().Be(1);
            sailback4.CardsForTrade.Should().Be(0);

            // Sailback collection identity 5 - should be imported with blank, a comment other fields using defaults
            var sailback5 = myCollectionInMemory.Single(c => c.Uuid == snappingSailbackUuid && c.SelectedLocationDisplayName == null && c.Comment == "my brother's, at least that's what he claims");
            sailback5.Name.Should().Be("Snapping Sailback");
            sailback5.SelectedFinish.Should().Be("nonfoil");
            sailback5.Language.Should().Be("English");
            sailback5.CardsOwned.Should().Be(1);
            sailback5.CardsForTrade.Should().Be(0);

            // Compare with database state to ensure it was correctly saved (spot check the same cards we checked in memory, and that the total count matches)
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            const string sql = @"
            SELECT uuid AS Uuids,
                   condition AS Conditions,
                   finish AS Finishes,
                   language AS Languages,
                   locationId AS LocationIds,
                   comment AS Comment,
                   cardsOwned AS CardsOwned,
                   cardsForTrade AS CardsForTrade
            FROM myCollection;
            ";

            using var cmd = new SQLiteCommand(sql, uow.CurrentConnection);
            using var reader = await cmd.ExecuteReaderAsync();
            var myCollectionDB = new List<CardSet>();

            while (await reader.ReadAsync())
            {
                myCollectionDB.Add(new CardSet
                {
                    Uuid = reader.GetString(0),
                    SelectedCondition = reader.GetString(1),
                    SelectedFinish = reader.GetString(2),
                    Language = reader.GetString(3),
                    SelectedLocationId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Comment = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CardsOwned = reader.GetInt32(6),
                    CardsForTrade = reader.GetInt32(7)
                });
            }

            const string locationSql = """
                    SELECT id, name, type
                    FROM cardLocations;
                    """;
            using var locationCmd = new SQLiteCommand(locationSql, uow.CurrentConnection);
            using var locationReader = await locationCmd.ExecuteReaderAsync();
            var cardLocations = new List<CardLocationRecord>();

            while (await locationReader.ReadAsync())
            {
                cardLocations.Add(new CardLocationRecord
                {
                    Id = locationReader.GetInt32(0),
                    Name = locationReader.GetString(1),
                    Type = locationReader.GetString(2)
                });
            }

            await uow.CommitAsync();

            myCollectionInMemory.Should().HaveCount(myCollectionDB.Count);

            var angel1Db = myCollectionDB.Single(c =>
                c.Uuid == angel1.Uuid &&
                c.SelectedCondition == angel1.SelectedCondition &&
                c.SelectedFinish == angel1.SelectedFinish &&
                c.Language == angel1.Language &&
                c.SelectedLocationId == angel1.SelectedLocationId &&
                c.Comment == angel1.Comment);

            angel1Db.CardsOwned.Should().Be(angel1.CardsOwned);
            angel1Db.CardsForTrade.Should().Be(angel1.CardsForTrade);

            var angel2Db = myCollectionDB.Single(c =>
                c.Uuid == angel2.Uuid &&
                c.SelectedCondition == angel2.SelectedCondition &&
                c.SelectedFinish == angel2.SelectedFinish &&
                c.Language == angel2.Language &&
                c.SelectedLocationId == angel2.SelectedLocationId &&
                c.Comment == angel2.Comment);

            angel2Db.CardsOwned.Should().Be(angel2.CardsOwned);
            angel2Db.CardsForTrade.Should().Be(angel2.CardsForTrade);

            var sailBack3Db = myCollectionDB.Single(c =>
                c.Uuid == sailback3.Uuid &&
                c.SelectedCondition == sailback3.SelectedCondition &&
                c.SelectedFinish == sailback3.SelectedFinish &&
                c.Language == sailback3.Language &&
                c.SelectedLocationId == sailback3.SelectedLocationId &&
                c.Comment == sailback3.Comment);

            sailBack3Db.CardsOwned.Should().Be(sailback3.CardsOwned);
            sailBack3Db.CardsForTrade.Should().Be(sailback3.CardsForTrade);

            var sailBack4Db = myCollectionDB.Single(c =>
                c.Uuid == sailback4.Uuid &&
                c.SelectedCondition == sailback4.SelectedCondition &&
                c.SelectedFinish == sailback4.SelectedFinish &&
                c.Language == sailback4.Language &&
                c.SelectedLocationId == sailback4.SelectedLocationId &&
                c.Comment == sailback4.Comment);

            sailBack4Db.CardsOwned.Should().Be(sailback4.CardsOwned);
            sailBack4Db.CardsForTrade.Should().Be(sailback4.CardsForTrade);

            var sailBack5Db = myCollectionDB.Single(c =>
                c.Uuid == sailback5.Uuid &&
                c.SelectedCondition == sailback5.SelectedCondition &&
                c.SelectedFinish == sailback5.SelectedFinish &&
                c.Language == sailback5.Language &&
                c.SelectedLocationId == sailback5.SelectedLocationId &&
                c.Comment == sailback5.Comment);

            sailBack5Db.CardsOwned.Should().Be(sailback5.CardsOwned);
            sailBack5Db.CardsForTrade.Should().Be(sailback5.CardsForTrade);

            // Check that the new locations were created in the database
            cardLocations.Should().HaveCount(4);
            cardLocations.Should().Contain(x => x.Name == "Aggro Fish" && x.Type == "Deck");
            cardLocations.Should().Contain(x => x.Name == "Binder 1" && x.Type == "Storage");
            cardLocations.Should().Contain(x => x.Name == "Binder 2" && x.Type == "Storage");
            cardLocations.Should().Contain(x => x.Name == "Binder 3" && x.Type == "Storage");

            // Check that the location ids are present in the imported cards in memory, and that they match the location records in the database
            var locationIds = new HashSet<int>(cardLocations.Select(x => x.Id));
            foreach (var card in _mainVM.MyCollectionVM.Cards.Where(c => c.SelectedLocationId.HasValue))
            {
                locationIds.Should().Contain(card.SelectedLocationId!.Value);
            }

            // =====================================================
            // Step 11 - Final
            // =====================================================
            var step11 = (ImportStep11_FinishViewModel)importVM.CurrentStepViewModel;
            await ImportScenarioTestsHelpers.EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep11_FinishViewModel && importVM.ProgressStep == "",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 11 should be active and progress label updated");
            step11.PrimaryActionButtonText.Should().Contain("OK");
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
    public static class ImportScenarioTestsHelpers
    {
        public static bool HasUuid(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidImportField", out var uuid) && !string.IsNullOrWhiteSpace(uuid);
        }
        public static bool HasUuids(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidsImportField", out var uuid) && !string.IsNullOrWhiteSpace(uuid);
        }
        public static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, string? because = null)
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
    }
}

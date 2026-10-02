using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.ModifyCollection;
using System.Windows.Input;

namespace CollectaMundo.Tests.ScenarioTests
{
    public sealed class Collection_Mutation_And_Event_Propagation_ScenarioTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private readonly InMemoryDatabaseFixture _fx = fx;
        private ScenarioTestContext _ctx = null!;
        public async ValueTask InitializeAsync()
        {
            _ctx = await ScenarioTestContext.CreateAsync(_fx);
        }
        public async ValueTask DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }
        [Fact]
        public async Task Collection_Mutation_And_Event_Propagation_Scenario()
        {
            #region ===== Section A: "Simple" test =====

            // Arrange: ManaValue > 1
            var numericFilter = _ctx.MainVM.FilterPanelVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Arrange: Rarity NOT (mythic OR rare)
            var rarityFilter = _ctx.MainVM.FilterPanelVM.Filters["Rarity"];
            foreach (var opt in rarityFilter.FilterOptions.Where(o => o.OptionName is "mythic" or "rare"))
            {
                opt.IsSelected = true;
            }

            rarityFilter.OperatorSelection = OperatorType.NOT;

            // Act
            ScenarioTestHelpers.ApplyAllFilters(_ctx.MainVM, _ctx.FilteringService);

            // Assert
            var expectedSummary = "Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _ctx.MainVM.FilterPanelVM.FilterSummary);
            Assert.Equal(23, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(17, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Arrange: Colors {R OR G}
            var colorFilter = _ctx.MainVM.FilterPanelVM.Filters["Colors"];
            foreach (var opt in colorFilter.FilterOptions.Where(o => o.OptionName is "R" or "G"))
            {
                opt.IsSelected = true;
            }

            colorFilter.OperatorSelection = OperatorType.OR;

            // Act
            ScenarioTestHelpers.ApplyAllFilters(_ctx.MainVM, _ctx.FilteringService);

            // Assert
            expectedSummary = "Colors: {R OR G} AND Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _ctx.MainVM.FilterPanelVM.FilterSummary);
            Assert.Equal(13, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(10, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Reset for main scenario
            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null);
            #endregion

            #region ===== Section B: text search by Name and setname =====

            // Act
            var nameFilter = _ctx.MainVM.FilterPanelVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Ranger";

            // Assert
            var expectedNames = new List<string> { "Boundary Lands Ranger", "Ranger-Captain of Eos // Ranger-Captain of Eos" }.OrderBy(n => n).ToList();

            var actualNames = _ctx.MainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n).ToList();

            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_ctx.MainVM.MyCollectionVM.FilteredCards);
            Assert.Equal(2, _ctx.MainVM.AllCardsVM.FilteredCards.Count);

            // Act: Reset by typing empty string
            nameFilter.SelectedSingleOption = "";

            // Assert
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            // Act: type "modern horizons" into SetName free text search
            var setNameFilter = (TestableFilterItemViewModel)_ctx.MainVM.FilterPanelVM.Filters["SetName"];
            setNameFilter.FreetextSearch = "modern horizons";
            setNameFilter.SimulateTypingComplete();

            // Assert
            Assert.Equal(9, _ctx.MainVM.AllCardsVM.FilteredCards.Count);

            // Act: Delete text to clear
            setNameFilter.FreetextSearch = "";

            // Assert
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            // Act: SetName = "Modern Horizons Art Series"
            setNameFilter.SelectedSingleOption = "Modern Horizons Art Series";

            // Assert
            Assert.Equal(3, _ctx.MainVM.AllCardsVM.FilteredCards.Count);

            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null);
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);
            #endregion

            #region ===== Section C: text + set filters =====

            // Arrange
            var rulesFilter = _ctx.MainVM.FilterPanelVM.Filters["Text"];

            // Act: Text contains nonsense string
            rulesFilter.SelectedSingleOption = "asdfasdf";

            // Assert
            Assert.Equal(0, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(0, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Clear rules text filter by pressing escape
            rulesFilter.HandleKeyLogic(Key.Escape);

            // Assert: cleared
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            // Act: Text type in "a" 
            rulesFilter.FreetextSearch = "a";
            rulesFilter.HandleKeyLogic(Key.Enter); // skip delay, apply immediately

            // Assert
            Assert.Equal(46, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal("Rulestext: \"a\"", _ctx.MainVM.FilterPanelVM.FilterSummary);
            Assert.Equal(21, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Press Backspace to remove "a"
            rulesFilter.FreetextSearch = rulesFilter.FreetextSearch[..^1];
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            // Act: Text contains “+1/+1 counter”
            rulesFilter.SelectedSingleOption = "+1/+1 counter";

            // Assert
            Assert.Equal(3, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Act: SetName contains "The List"
            var setFilter = _ctx.MainVM.FilterPanelVM.Filters["SetName"];
            setFilter.SelectedSingleOption = "The List";
            _ctx.MainVM.FilterPanelVM.NotifyFilterChanged();

            // Assert
            Assert.Equal(2, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);
            Assert.Equal("Set Name: \"The List\" AND Rulestext: \"+1/+1 counter\"", _ctx.MainVM.FilterPanelVM.FilterSummary);

            // Reset
            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null);

            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);
            #endregion

            #region ===== Section D: types + supertypes =====

            // Arrange: Types {Creature OR Planeswalker}
            var typesFilter = _ctx.MainVM.FilterPanelVM.Filters["Types"];
            foreach (var opt in typesFilter.FilterOptions.Where(o => o.OptionName is "Creature" or "Planeswalker"))
            {
                opt.IsSelected = true;
            }

            typesFilter.OperatorSelection = OperatorType.OR;

            // Assert
            Assert.Equal(29, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(10, _ctx.MainVM.MyCollectionVM.FilteredCards.Count);

            // Arrange: SuperTypes {Legendary}
            var superTypesFilter = _ctx.MainVM.FilterPanelVM.Filters["SuperTypes"];
            foreach (var opt in superTypesFilter.FilterOptions.Where(o => o.OptionName is "Legendary"))
            {
                opt.IsSelected = true;
            }

            // Assert
            Assert.Equal(6, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            Assert.Empty(_ctx.MainVM.MyCollectionVM.FilteredCards);
            Assert.Equal("Supertypes: {Legendary} AND Card type: {Creature OR Planeswalker}", _ctx.MainVM.FilterPanelVM.FilterSummary);
            #endregion

            #region ===== Section E: add one card (Karox) via AddSelectedCards =====

            // Arrange
            const string uuidKarox = "e4dcfe4f-8441-5eec-9f74-a7b3672e90e0";
            var karox = ScenarioTestHelpers.FindCard(_ctx.MainVM.AllCardsVM.FilteredCards, uuidKarox);

            // Act
            _ctx.MainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { karox });

            // Assert: staged
            Assert.Single(_ctx.MainVM.AddCardsVM.CardsToAddOrEdit, c => c.CardToAddOrEdit.Uuid == uuidKarox);

            // Act: submit
            _ctx.MainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: now in MyCollection
            Assert.Equal(23, _ctx.MainVM.MyCollectionVM.Cards.Count);
            #endregion

            #region ===== Section F: add Sokrates with field edits =====

            // Act: filter by name "sokrates"
            nameFilter.SelectedSingleOption = "sokrates";

            // Assert
            expectedNames = [.. new[] { "Sokrates, Athenian Teacher" }.OrderBy(n => n)];
            actualNames = [.. _ctx.MainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n)];
            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_ctx.MainVM.MyCollectionVM.FilteredCards);

            // Arrange
            const string uuidSokrates = "3c389f9c-e459-5b16-87b5-d51644f05b25";
            var sokrates = ScenarioTestHelpers.FindCard(_ctx.MainVM.AllCardsVM.FilteredCards, uuidSokrates);
            // Act: stage Sokrates
            _ctx.MainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { sokrates });

            // Assert: staged
            Assert.Single(_ctx.MainVM.AddCardsVM.CardsToAddOrEdit, c => c.CardToAddOrEdit.Uuid == uuidSokrates);

            // Arrange: modify before submit
            var pending = _ctx.MainVM.AddCardsVM.CardsToAddOrEdit.Single(c => c.CardToAddOrEdit.Uuid == uuidSokrates);
            pending.SelectedCondition = "Played";
            pending.CardsForTrade = 1;

            // Act: submit
            _ctx.MainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: now in MyCollection with edits
            Assert.Equal(24, _ctx.MainVM.MyCollectionVM.Cards.Count);

            var sokratesInCollection = ScenarioTestHelpers.FindCard(_ctx.MainVM.MyCollectionVM.Cards, uuidSokrates);
            Assert.Equal("Played", sokratesInCollection.SelectedCondition);
            Assert.Equal(1, sokratesInCollection.CardsForTrade);

            // Assert: staging cleared
            Assert.Empty(_ctx.MainVM.AddCardsVM.CardsToAddOrEdit);

            //// Assert: filter facets include new values
            var conditionFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedCondition"];
            Assert.Contains("Played", conditionFilter.AvailableOptions);

            var languageFilter = _ctx.MainVM.FilterPanelVM.Filters["Language"];
            Assert.Contains("Ancient Greek", languageFilter.AvailableOptions);
            #endregion

            #region ===== Section G: delete two specific cards (etched + German) =====

            // Arrange
            const string uuidEtched = "0add0930-720f-5bf5-bcf5-ee208eeb9040"; // Once Upon a Time (etched)
            const string uuidGerman = "5e6a3099-2597-5755-8a6f-67f1569a3b8a"; // Leave No Trace (German)

            var etchedCard = ScenarioTestHelpers.FindCard(_ctx.MainVM.MyCollectionVM.Cards, uuidEtched);
            var germanCard = ScenarioTestHelpers.FindCard(_ctx.MainVM.MyCollectionVM.Cards, uuidGerman);

            var deletionSelection = new object[] { etchedCard, germanCard };

            // Act
            _ctx.MainVM.AddCardsVM.DeleteSelectedCardsCommand.Execute(deletionSelection);

            // Wait defensively for async path to complete
            await StatusTestDriver.WaitUntilAsync(() => !_ctx.MainVM.MyCollectionVM.Cards.Any(c => c.Uuid == uuidEtched || c.Uuid == uuidGerman), "Deleted cards were not removed from the collection.");

            // Assert: removed from collection
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.Uuid == uuidEtched);
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.Uuid == uuidGerman);
            // Assert: facets updated (ImmediateScheduler makes this synchronous)
            var finishFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedFinish"];
            Assert.DoesNotContain(finishFilter.AvailableOptions, s => string.Equals(s, "etched", StringComparison.OrdinalIgnoreCase));

            var langFilter = _ctx.MainVM.FilterPanelVM.Filters["Language"];
            Assert.DoesNotContain(langFilter.AvailableOptions, s => string.Equals(s, "German", StringComparison.OrdinalIgnoreCase));

            // Assert: count back to 22
            Assert.Equal(22, _ctx.MainVM.MyCollectionVM.Cards.Count);
            #endregion

            #region ===== Section H: merge scenario (Hypnotic Cloud defaults) =====

            // Arrange
            const string uuidMerge = "413e11a5-35a1-51c7-928b-219b4453a094"; // Hypnotic Cloud
            var toMerge = _ctx.MainVM.AllCardsVM.Cards.Single(c => c.Uuid == uuidMerge);
            var mergeSelection = new object[] { toMerge };

            // Act
            _ctx.MainVM.AddCardsVM.SubmitNewCardsWithDefaultsCommand.Execute(mergeSelection);

            // Assert: still 22 after merge
            Assert.Equal(22, _ctx.MainVM.MyCollectionVM.Cards.Count);

            // Assert: merged survivor has the incremented total (VM + DB agree)
            const string cond = "Near Mint";
            const string lang = "English";
            const string finish = "nonfoil";

            static int OwnedTotal(IEnumerable<CollectionCard> list) =>
                list.Where(c => c.Uuid == uuidMerge &&
                                string.Equals(c.SelectedCondition, cond, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Language, lang, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.SelectedFinish, finish, StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.CardsOwned);

            var ownedVm = OwnedTotal(_ctx.MainVM.MyCollectionVM.Cards);

            var survivor = _ctx.MainVM.MyCollectionVM.Cards.Single(c =>
                c.Uuid == uuidMerge &&
                string.Equals(c.SelectedCondition, cond, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Language, lang, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.SelectedFinish, finish, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(ownedVm, survivor.CardsOwned);

            int sumOwnedDb = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT SUM(cardsOwned)
                FROM myCollection
                WHERE uuid = @uuid
                  AND condition = @cond
                  AND language = @lang
                  AND finish = @finish;
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@uuid", uuidMerge);
                    cmd.Parameters.AddWithValue("@cond", cond);
                    cmd.Parameters.AddWithValue("@lang", lang);
                    cmd.Parameters.AddWithValue("@finish", finish);
                });

            int sumTradeDb = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT SUM(cardsForTrade)
                FROM myCollection
                WHERE uuid = @uuid
                  AND condition = @cond
                  AND language = @lang
                  AND finish = @finish;
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@uuid", uuidMerge);
                    cmd.Parameters.AddWithValue("@cond", cond);
                    cmd.Parameters.AddWithValue("@lang", lang);
                    cmd.Parameters.AddWithValue("@finish", finish);
                });

            Assert.Equal(ownedVm, sumOwnedDb);
            #endregion

            #region ===== Section I: Check keyword aggregation from b-side of card =====
            // Reset
            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null);

            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            // Arrange
            _ctx.MainVM.FilterPanelVM.Filters["Keywords"].FilterOptions.FirstOrDefault(o => o.OptionName == "Vigilance")!.IsSelected = true;
            expectedNames = [.. new List<string> { "Bruna, the Fading Light // Brisela, Voice of Nightmares", "Gisela, the Broken Blade // Brisela, Voice of Nightmares" }.OrderBy(n => n)];
            actualNames = [.. _ctx.MainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n)];

            // Assert
            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_ctx.MainVM.MyCollectionVM.FilteredCards);
            Assert.Equal(2, _ctx.MainVM.AllCardsVM.FilteredCards.Count);
            #endregion

            #region ===== Section J: location CRUD + assign/remove location through collection mutation flow =====

            // Arrange
            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null);
            ScenarioTestHelpers.AssertFiltersCleared(_ctx.MainVM);

            var locationVm = _ctx.MainVM.CardLocationVM;

            // Act: create location
            locationVm.LocationName = "Scenario Test Deck";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitCommand.ExecuteAsync(null);

            // Assert: location exists in utility VM
            var scenarioLocation = locationVm.Locations.Single(l => l.Name == "Scenario Test Deck");
            Assert.Equal(CardLocationType.Deck, scenarioLocation.Type);

            // Arrange: choose a stable existing collection card
            var targetCard = _ctx.MainVM.MyCollectionVM.Cards.First();
            var targetCardId = targetCard.CardId;

            // Act: set location through existing collection mutation pipeline
            var param = new SetLocationForSelectedCardsParameter(new object[] { targetCard }, scenarioLocation.Id);

            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(param);

            // Assert: VM card has location
            var updatedTarget = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == targetCardId);
            Assert.Equal(scenarioLocation.Id, updatedTarget.SelectedLocationId);
            Assert.Equal("Scenario Test Deck", updatedTarget.SelectedLocationName);
            Assert.Equal("Deck: Scenario Test Deck", updatedTarget.SelectedLocationDisplayName);

            // Assert: DB card has location
            var locationId = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT locationId
                FROM myCollection
                WHERE id = @id;
                """,
                cmd => cmd.Parameters.AddWithValue("@id", targetCardId));

            Assert.Equal(scenarioLocation.Id, locationId);

            // Act: delete location
            locationVm.SelectedItems.Clear();
            locationVm.SelectedItems.Add(scenarioLocation);

            locationVm.DeleteSelectedLocationsCommand.Execute(null); // first click activates confirmation
            locationVm.DeleteSelectedLocationsCommand.Execute(null); // second click confirms

            // Assert: location removed from utility VM
            Assert.DoesNotContain(locationVm.Locations, l => l.Id == scenarioLocation.Id);

            // Assert: VM card location is cleared after delete
            var clearedTarget = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == targetCardId);
            Assert.Null(clearedTarget.SelectedLocationId);
            Assert.Null(clearedTarget.SelectedLocationName);
            Assert.Null(clearedTarget.SelectedLocationDisplayName);

            // Assert: DB card location is cleared
            var clearedLocationId = await ScenarioTestHelpers.ExecuteScalarAsync<int?>(_ctx.DbFactory,
                """
                SELECT locationId
                FROM myCollection
                WHERE id = @id;
                """,
                cmd => cmd.Parameters.AddWithValue("@id", targetCardId));

            Assert.Null(clearedLocationId);
            #endregion

            #region ===== Section K1: add multiple otters with different location/comment identities =====

            // Act: create scenario locations
            locationVm.LocationName = "Box 1";
            locationVm.SelectedLocationType = CardLocationType.Storage;
            await locationVm.SubmitCommand.ExecuteAsync(null);

            locationVm.LocationName = "Box 2";
            locationVm.SelectedLocationType = CardLocationType.Storage;
            await locationVm.SubmitCommand.ExecuteAsync(null);
            locationVm.LocationName = "Deck Awesome!";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitCommand.ExecuteAsync(null);

            // Assert: locations exist
            var box1 = locationVm.Locations.Single(l => l.Name == "Box 1");
            var box2 = locationVm.Locations.Single(l => l.Name == "Box 2");
            var deckAwesome = locationVm.Locations.Single(l => l.Name == "Deck Awesome!");

            Assert.Equal(CardLocationType.Storage, box1.Type);
            Assert.Equal(CardLocationType.Storage, box2.Type);
            Assert.Equal(CardLocationType.Deck, deckAwesome.Type);

            // Arrange: add five otters with different collection identities
            const string uuidOtter = "49481296-5e87-500b-9d95-8011f432466a";
            var otter = ScenarioTestHelpers.FindCard(_ctx.MainVM.AllCardsVM.Cards, uuidOtter);

            _ctx.MainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { otter, otter, otter, otter, otter });

            var pendingOtters = _ctx.MainVM.AddCardsVM.CardsToAddOrEdit.Where(r => r.CardToAddOrEdit.Uuid == uuidOtter).ToList();

            Assert.Equal(5, pendingOtters.Count);

            // Otter 1: Box 1
            pendingOtters[0].SelectedLocationId = box1.Id;

            // Otter 2: Box 2
            pendingOtters[1].SelectedLocationId = box2.Id;

            // Otter 3: Box 2 + comment
            pendingOtters[2].SelectedLocationId = box2.Id;
            pendingOtters[2].Comment = "smudgemark";

            // Otter 4: Deck Awesome!
            pendingOtters[3].SelectedLocationId = deckAwesome.Id;

            // Otter 5: no location, no comment
            pendingOtters[4].SelectedLocationId = null;
            pendingOtters[4].Comment = null;

            // Act: submit otters
            _ctx.MainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: five distinct otter rows were added
            Assert.Equal(27, _ctx.MainVM.MyCollectionVM.Cards.Count);

            var ottersInCollection = _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter).ToList();

            Assert.Equal(5, ottersInCollection.Count);

            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));
            #endregion

            #region ===== Section K2: Deck Builder reflects physical deck allocation =====

            // Arrange: refresh Deck Management so the newly created Deck Awesome! location is available.
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();

            var deckAwesomeDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(d => d.LocationId == deckAwesome.Id);

            _ctx.MainVM.DeckManagementVM.SelectedItem = deckAwesomeDeck;

            // Act: enter Deck Builder.
            _ctx.MainVM.DeckManagementVM.EnterDeckBuilderCommand.Execute(null);

            await StatusTestDriver.WaitUntilAsync(() => _ctx.MainVM.DeckBuilderVM.DeckLocationId == deckAwesome.Id, "Deck Builder did not finish loading Deck Awesome.");

            var deckBuilder = _ctx.MainVM.DeckBuilderVM;

            // Deck Awesome currently has no desired deck contents.
            Assert.Empty(deckBuilder.MainboardZone.Cards);

            var oracleOtter = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Otter");

            // Add Otter to the desired deck so its collection/deckbox quantities are projected onto a DeckCardEntryViewModel.
            await deckBuilder.AddCardToDeckCommand.ExecuteAsync(new object[] { oracleOtter });

            var otterDeckRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Otter");

            // Five physical copies exist in the collection.
            Assert.Equal(5, otterDeckRow.OwnedQuantity);

            // At this point only one physical Otter is allocated to Deck Awesome.
            Assert.Equal(1, otterDeckRow.AllocatedQuantity);

            // Copies allocated to this deck remain available to this deck.
            Assert.Equal(5, otterDeckRow.AvailableQuantity);

            Assert.False(otterDeckRow.HasInsufficientAvailableQuantity);

            // Return through the normal Deck Builder navigation.
            deckBuilder.BackToDeckManagementCommand.Execute(null);

            Assert.Same(_ctx.MainVM.DeckManagementVM, _ctx.MainVM.PagesDecksHostVM.CurrentDecksContentViewModel);

            #endregion

            #region ===== Section L: edit otter location to none and merge with existing no-location otter =====

            // Arrange: find Otter 1 with Box 1 and the existing no-location/no-comment otter
            var otterBox1 = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterNoLocationBefore = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));

            var otterBox1Id = otterBox1.CardId;
            var otterNoLocationId = otterNoLocationBefore.CardId;
            var expectedMergedOwned = otterBox1.CardsOwned + otterNoLocationBefore.CardsOwned;
            var expectedMergedTrade = otterBox1.CardsForTrade + otterNoLocationBefore.CardsForTrade;

            // Act: stage Otter 1 for edit
            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterBox1 });

            var pendingOtterEdit = _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit.Single(r => r.CardToAddOrEdit.CardId == otterBox1Id);

            // Act: clear location in edit row
            pendingOtterEdit.SelectedLocationId = null;

            // Act: submit edit
            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SubmitCardEditsCommand.Execute(null);

            // Assert: collection row count decreased by one due to merge
            Assert.Equal(26, _ctx.MainVM.MyCollectionVM.Cards.Count);

            // Assert: Box 1 otter row was removed
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.CardId == otterBox1Id);

            // Assert: no-location otter survivor remains and has merged quantities
            var otterNoLocationAfter = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == otterNoLocationId);

            Assert.Equal(uuidOtter, otterNoLocationAfter.Uuid);
            Assert.Null(otterNoLocationAfter.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(otterNoLocationAfter.Comment));
            Assert.Equal(expectedMergedOwned, otterNoLocationAfter.CardsOwned);
            Assert.Equal(expectedMergedTrade, otterNoLocationAfter.CardsForTrade);

            // Refresh otters list
            ottersInCollection = [.. _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];
            Assert.Equal(4, ottersInCollection.Count); // One has been merged away, so now 4 distinct otter rows instead of 5

            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);
            #endregion

            #region ===== Section M: edit two Box 2 otters into same new Box 1 identity =====

            // Arrange: find the two Box 2 otters
            var otterBox2NoComment = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterBox2Smudge = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");

            var otterBox2NoCommentId = otterBox2NoComment.CardId;
            var otterBox2SmudgeId = otterBox2Smudge.CardId;

            var expectedBox1Owned = otterBox2NoComment.CardsOwned + otterBox2Smudge.CardsOwned;
            var expectedBox1Trade = otterBox2NoComment.CardsForTrade + otterBox2Smudge.CardsForTrade;

            // Act: stage both Box 2 otters for edit
            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterBox2NoComment, otterBox2Smudge });

            var pendingBox2Edits = _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit.Where(r => r.CardToAddOrEdit.CardId == otterBox2NoCommentId || r.CardToAddOrEdit.CardId == otterBox2SmudgeId).ToList();

            Assert.Equal(2, pendingBox2Edits.Count);

            // Act: change both to Box 1 and no comment
            foreach (var pendingEdit in pendingBox2Edits)
            {
                pendingEdit.SelectedLocationId = box1.Id;
                pendingEdit.Comment = null;
            }

            // Act: submit edits
            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SubmitCardEditsCommand.Execute(null);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(25, _ctx.MainVM.MyCollectionVM.Cards.Count);

            // Refresh otters list
            ottersInCollection = [.. _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];
            Assert.Equal(3, ottersInCollection.Count);

            // Assert: one Box 1/no-comment otter identity remains with merged quantities
            var otterBox1Merged = ottersInCollection.Single(c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));

            Assert.Equal(expectedBox1Owned, otterBox1Merged.CardsOwned);
            Assert.Equal(expectedBox1Trade, otterBox1Merged.CardsForTrade);

            // Assert: old Box 2 identities are gone
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");

            // Assert: remaining otter identities are the expected ones
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);

            // Assert: DB matches VM truth for otter rows
            var dbRows = await ScenarioTestHelpers.ExecuteQueryAsync<(int Id, int? LocationId, string? Comment, int Owned, int Trade)>(
                _ctx.DbFactory,
                """
                SELECT id, uuid, locationId, comment, cardsOwned, cardsForTrade
                FROM myCollection
                WHERE uuid = @uuid
                ORDER BY id;
                """,
                reader =>
                {
                    var locationOrdinal = reader.GetOrdinal("locationId");
                    var commentOrdinal = reader.GetOrdinal("comment");

                    return (
                        Id: reader.GetInt32(reader.GetOrdinal("id")),
                        LocationId: reader.IsDBNull(locationOrdinal)
                            ? null
                            : reader.GetInt32(locationOrdinal),
                        Comment: reader.IsDBNull(commentOrdinal)
                            ? null
                            : reader.GetString(commentOrdinal),
                        Owned: reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                        Trade: reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                    );
                },
                cmd => cmd.Parameters.AddWithValue("@uuid", uuidOtter));

            Assert.Equal(3, dbRows.Count);
            Assert.Contains(dbRows, r => r.LocationId == box1.Id && string.IsNullOrWhiteSpace(r.Comment) && r.Owned == expectedBox1Owned && r.Trade == expectedBox1Trade);
            Assert.Contains(dbRows, r => r.LocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(r.Comment));
            Assert.Contains(dbRows, r => r.LocationId is null && string.IsNullOrWhiteSpace(r.Comment) && r.Owned == 2);
            Assert.DoesNotContain(dbRows, r => r.LocationId == box2.Id);
            #endregion

            #region ===== Section N: deleting location merges staged row and reconciles edit list =====

            // Arrange: stage Deck Awesome otter for edit
            var otterDeckAwesome = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterDeckAwesomeId = otterDeckAwesome.CardId;

            var otterNoLocationBeforeDelete = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));

            otterNoLocationId = otterNoLocationBeforeDelete.CardId;
            var expectedNoLocationOwnedAfterDelete = otterNoLocationBeforeDelete.CardsOwned + otterDeckAwesome.CardsOwned;
            var expectedNoLocationTradeAfterDelete = otterNoLocationBeforeDelete.CardsForTrade + otterDeckAwesome.CardsForTrade;

            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterDeckAwesome });

            // Assert: staged before location delete
            Assert.Contains(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit, r => r.CardToAddOrEdit.CardId == otterDeckAwesomeId);

            // Act: delete Deck Awesome location
            locationVm.SelectedItems.Clear();
            locationVm.SelectedItems.Add(deckAwesome);

            await locationVm.DeleteSelectedLocationsCommand.ExecuteAsync(null); // activate confirmation
            await locationVm.DeleteSelectedLocationsCommand.ExecuteAsync(null); // confirm delete

            // Assert: staged Deck Awesome otter was removed because its source row was merged away
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit, r => r.CardToAddOrEdit.CardId == otterDeckAwesomeId);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(24, _ctx.MainVM.MyCollectionVM.Cards.Count);

            // Refresh otters list
            ottersInCollection = [.. _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            // Assert: now only two otter collection rows remain
            Assert.Equal(2, ottersInCollection.Count);

            // Assert: Deck Awesome otter row was removed
            Assert.DoesNotContain(ottersInCollection, c => c.CardId == otterDeckAwesomeId);
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id);

            // Assert: no-location/no-comment survivor absorbed Deck Awesome otter
            var otterNoLocationAfterDelete = ottersInCollection.Single(c => c.CardId == otterNoLocationId);

            Assert.Null(otterNoLocationAfterDelete.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(otterNoLocationAfterDelete.Comment));
            Assert.Equal(expectedNoLocationOwnedAfterDelete, otterNoLocationAfterDelete.CardsOwned);
            Assert.Equal(expectedNoLocationTradeAfterDelete, otterNoLocationAfterDelete.CardsForTrade);

            // Assert: Box 1 otter still exists
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 3);
            #endregion

            #region ===== Section O: simulated right-click set location merges remaining otters into Deck Awesome identity =====

            // Arrange: recreate Deck Awesome because it was deleted in previous section
            locationVm.LocationName = "Deck Awesome!";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitCommand.ExecuteAsync(null);

            deckAwesome = locationVm.Locations.Single(l => l.Name == "Deck Awesome!");

            ottersInCollection = [.. _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            Assert.Equal(2, ottersInCollection.Count);

            var expectedDeckOwned = ottersInCollection.Sum(c => c.CardsOwned);
            var expectedDeckTrade = ottersInCollection.Sum(c => c.CardsForTrade);

            // Act: simulate right-click command on the two remaining otters
            var setDeckParam = new SetLocationForSelectedCardsParameter(ottersInCollection.Cast<object>().ToArray(), deckAwesome.Id);

            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(setDeckParam);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(23, _ctx.MainVM.MyCollectionVM.Cards.Count);

            // Assert: all otters merged into one Deck Awesome identity
            ottersInCollection = [.. _ctx.MainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            var finalOtter = Assert.Single(ottersInCollection);

            Assert.Equal(deckAwesome.Id, finalOtter.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(finalOtter.Comment));
            Assert.Equal(5, finalOtter.CardsOwned);
            Assert.Equal(expectedDeckOwned, finalOtter.CardsOwned);
            Assert.Equal(expectedDeckTrade, finalOtter.CardsForTrade);
            Assert.Equal("Deck: Deck Awesome!", finalOtter.SelectedLocationDisplayName);

            // Assert: DB truth matches VM after right-click location merge
            dbRows = await ScenarioTestHelpers.ExecuteQueryAsync<(int Id, int? LocationId, string? Comment, int Owned, int Trade)>(
                _ctx.DbFactory,
                """
                SELECT id, locationId, comment, cardsOwned, cardsForTrade
                FROM myCollection
                WHERE uuid = @uuid;
                """,
                reader =>
                {
                    var locationOrdinal = reader.GetOrdinal("locationId");
                    var commentOrdinal = reader.GetOrdinal("comment");

                    return (
                        Id: reader.GetInt32(reader.GetOrdinal("id")),
                        LocationId: reader.IsDBNull(locationOrdinal)
                            ? null
                            : reader.GetInt32(locationOrdinal),
                        Comment: reader.IsDBNull(commentOrdinal)
                            ? null
                            : reader.GetString(commentOrdinal),
                        Owned: reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                        Trade: reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                    );
                },
                cmd => cmd.Parameters.AddWithValue("@uuid", uuidOtter));

            var (Id, LocationId, Comment, Owned, Trade) = Assert.Single(dbRows);

            Assert.Equal(deckAwesome.Id, LocationId);
            Assert.True(string.IsNullOrWhiteSpace(Comment));
            Assert.Equal(5, Owned);
            Assert.Equal(expectedDeckTrade, Trade);

            #endregion

            #region ===== Section P: Deck Builder reflects final physical allocation =====

            // Arrange: refresh Deck Management so the recreated Deck Awesome! is available.
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();

            var recreatedDeckAwesome = _ctx.MainVM.DeckManagementVM.Decks.Single(d => d.LocationId == deckAwesome.Id);

            _ctx.MainVM.DeckManagementVM.SelectedItem = recreatedDeckAwesome;

            // Act: enter the recreated deck.
            _ctx.MainVM.DeckManagementVM.EnterDeckBuilderCommand.Execute(null);

            await StatusTestDriver.WaitUntilAsync(() => _ctx.MainVM.DeckBuilderVM.DeckLocationId == deckAwesome.Id, "Deck Builder did not finish loading the recreated Deck Awesome.");

            deckBuilder = _ctx.MainVM.DeckBuilderVM;

            // This is a new deck/location. The desired Otter row belonging to the previously deleted Deck Awesome must not have followed it by name.
            Assert.Empty(deckBuilder.MainboardZone.Cards);

            // Act: add Otter to the new desired deck.
            await deckBuilder.AddCardToDeckCommand.ExecuteAsync(new object[] { oracleOtter });

            otterDeckRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Otter");

            // All five copies still physically exist.
            Assert.Equal(5, otterDeckRow.OwnedQuantity);

            // All five are now physically allocated to this Deck Awesome location.
            Assert.Equal(5, otterDeckRow.AllocatedQuantity);

            // Allocation to this same deck does not reduce availability for this deck.
            Assert.Equal(5, otterDeckRow.AvailableQuantity);
            Assert.False(otterDeckRow.HasInsufficientAvailableQuantity);

            #endregion

            #region ===== Section Q: deck export availability and Cardmarket wants list =====

            // Arrange: Deck Awesome is still loaded in Deck Builder from the previous section. It currently has one desired Otter.

            // Increase desired quantity from 1 to 6.
            // AddPlaySet adds four: 1 -> 5.
            await deckBuilder.AddPlaySetToDeckCommand.ExecuteAsync(new object[] { oracleOtter });

            // Add one more: 5 -> 6.
            await deckBuilder.AddCardToDeckCommand.ExecuteAsync(new object[] { oracleOtter });

            otterDeckRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Otter");

            Assert.Equal(6, otterDeckRow.DesiredQuantity);

            // Physical collection state remains:
            // Owned = 5
            // Allocated here = 5
            // Available to this deck = 5
            Assert.Equal(5, otterDeckRow.OwnedQuantity);
            Assert.Equal(5, otterDeckRow.AllocatedQuantity);
            Assert.Equal(5, otterDeckRow.AvailableQuantity);

            // With desired 6 and available 5, the deck is short exactly one copy.
            Assert.True(otterDeckRow.HasInsufficientAvailableQuantity);

            // Return to Deck Management.
            deckBuilder.BackToDeckManagementCommand.Execute(null);

            Assert.Same(_ctx.MainVM.DeckManagementVM, _ctx.MainVM.PagesDecksHostVM.CurrentDecksContentViewModel);

            var deckManagement = _ctx.MainVM.DeckManagementVM;

            // Select Deck Awesome so export availability is recalculated.
            deckManagement.SelectedItem = recreatedDeckAwesome;

            // Availability is calculated asynchronously after selection.
            await StatusTestDriver.WaitUntilAsync(() => deckManagement.CanExportCsv && deckManagement.CanGenerateWantList, "Deck export availability did not refresh for Deck Awesome.");

            // A non-empty deck can be exported as CSV.
            Assert.True(deckManagement.CanExportCsv);

            // The one-copy shortage makes Cardmarket generation available.
            Assert.True(deckManagement.CanGenerateWantList);

            // Act: generate the Cardmarket wants list.
            await deckManagement.GenerateWantListCommand.ExecuteAsync(null);

            // Assert: shortage calculation flowed all the way through the export stack.
            Assert.Equal("1 Otter", deckManagement.WantListText);

            Assert.Equal("Cardmarket wants list generated.", deckManagement.StatusMessage);

            #endregion
        }
    }
}


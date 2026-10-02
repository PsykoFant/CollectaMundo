using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.ModifyCollection;

namespace CollectaMundo.Tests.ScenarioTests
{
    public sealed class LocationAndDeckManagementScenarioTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
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
        public async Task Location_And_Deck_Management_Scenario()
        {
            #region Test 1 - Happy path create

            // Arrange: load managers used by this scenario
            _ctx.MainVM.TopMenuVM.ShowDecksPageCommand.Execute(null);
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            // Assert initial state
            Assert.Single(_ctx.MainVM.DeckManagementVM.Decks);

            var existingDeck = _ctx.MainVM.DeckManagementVM.Decks.Single();

            Assert.Equal("Aggro Fish", existingDeck.Name);
            Assert.True(string.IsNullOrWhiteSpace(existingDeck.Format));
            Assert.True(string.IsNullOrWhiteSpace(existingDeck.Description));
            Assert.Equal(string.Empty, existingDeck.FormatDisplayName);

            Assert.Contains(_ctx.MainVM.DeckManagementVM.DeckFormats, x => x.DisplayName == "Casual/kitchen table");
            Assert.Contains(_ctx.MainVM.DeckManagementVM.DeckFormats, x => x.DisplayName == "Commander");
            Assert.Contains(_ctx.MainVM.CardLocationVM.Locations, x => x.Name == "Aggro Fish" && x.Type == CardLocationType.Deck);

            // Create new deck
            _ctx.MainVM.DeckManagementVM.DeckName = "Control Shell";
            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = "commander";
            _ctx.MainVM.DeckManagementVM.Description = "Blue-white control deck";

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);

            // Assert deck manager state
            var createdDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Control Shell");
            Assert.Equal("commander", createdDeck.Format);
            Assert.Equal("Blue-white control deck", createdDeck.Description);
            Assert.Equal("Commander", createdDeck.FormatDisplayName);

            Assert.Equal(2, _ctx.MainVM.DeckManagementVM.Decks.Count);

            // Assert location manager state
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync(); // First, simulate navigation to location managerto ensure location list is refreshed after deck creation
            var createdLocation = _ctx.MainVM.CardLocationVM.Locations.Single(x => x.Name == "Control Shell");
            Assert.Equal(CardLocationType.Deck, createdLocation.Type);

            // Assert persisted location
            var count = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                                """
                                SELECT COUNT(*)
                                FROM cardLocations
                                WHERE name = 'Control Shell'
                                  AND type = 'Deck';
                                """);

            Assert.Equal(1, count);

            // Assert persisted metadata
            var deckRows = await ScenarioTestHelpers.ExecuteQueryAsync<(string Format, string Description)>(_ctx.DbFactory,
                """
                SELECT format, description
                FROM myDecks d
                INNER JOIN cardLocations l
                    ON l.id = d.locationId
                WHERE l.name = @name;
                """,
                reader => (
                    Format: reader.GetString(reader.GetOrdinal("format")),
                    Description: reader.GetString(reader.GetOrdinal("description"))
                ),
                cmd => cmd.Parameters.AddWithValue("@name", "Control Shell"));

            var (Format, Description) = Assert.Single(deckRows);

            Assert.Equal("commander", Format);
            Assert.Equal("Blue-white control deck", Description);

            // Assign deck to collection card through right - click command
            var cardToUpdate = _ctx.MainVM.MyCollectionVM.Cards.First(c => c.SelectedLocationId is null);
            var setLocationParam = new SetLocationForSelectedCardsParameter(new object[] { cardToUpdate }, createdLocation.Id);

            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(setLocationParam);

            var updatedCard = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == cardToUpdate.CardId);

            Assert.Equal(createdLocation.Id, updatedCard.SelectedLocationId);

            // Filter on deck location
            _ctx.MainVM.FilterPanelVM.ClearFiltersCommand?.Execute(null); // clear other filters to isolate location filter behavior

            var locationFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedLocationDisplayName"];

            locationFilter.FilterOptions.Single(o => o.OptionName == "Deck: Control Shell").IsSelected = true;
            ScenarioTestHelpers.ApplyAllFilters(_ctx.MainVM, _ctx.FilteringService);

            var filteredCard = _ctx.MainVM.MyCollectionVM.FilteredCards.Single();

            Assert.Equal(updatedCard.CardId, filteredCard.CardId);
            Assert.Equal(createdLocation.Id, filteredCard.SelectedLocationId);
            Assert.Equal("Deck: Control Shell", filteredCard.SelectedLocationDisplayName);

            #endregion

            #region Test 2 - Build initial desired deck
            // Arrange: select the newly created Commander deck.
            _ctx.MainVM.DeckManagementVM.SelectedItem = createdDeck;

            Assert.True(_ctx.MainVM.DeckManagementVM.IsEnterDeckBuilderButtonEnabled);
            Assert.False(_ctx.MainVM.DeckManagementVM.CanExportCsv);
            Assert.False(_ctx.MainVM.DeckManagementVM.CanGenerateWantList);

            // EnterDeckBuilder raises an event whose handler asynchronously loads the deck.
            // Wait until DeckBuilderVM has finished initializing this deck.
            _ctx.MainVM.DeckManagementVM.EnterDeckBuilderCommand.Execute(null);
            await StatusTestDriver.WaitUntilAsync(() => _ctx.MainVM.DeckBuilderVM.DeckLocationId == createdDeck.LocationId, "Deck Builder did not finish loading the selected deck.");
            var deckBuilder = _ctx.MainVM.DeckBuilderVM;

            // Assert: deck identity / format flowed from Deck Management into Deck Builder.
            Assert.Equal(createdDeck.LocationId, deckBuilder.DeckLocationId);
            Assert.Equal("Control Shell", deckBuilder.DeckName);
            Assert.Equal("commander", deckBuilder.DeckFormat);
            Assert.Equal("Commander", deckBuilder.DeckFormatDisplayName);

            Assert.False(deckBuilder.IsCommanderZoneVisible);
            Assert.False(deckBuilder.IsSideboardZoneVisible);

            // Arrange: use the already-materialized Oracle card projection.
            var sokrates = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Sokrates, Athenian Teacher");
            var plains = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Plains");
            var prismaticEnding = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Prismatic Ending");
            var leaveNoTrace = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Leave No Trace");
            var deftbladeElite = _ctx.MainVM.OracleCardsVM.Cards.Single(c => c.Name == "Deftblade Elite");

            // Act: set commander.
            deckBuilder.SelectedOracleCard = sokrates;
            await deckBuilder.SetCardAsCommanderCommand.ExecuteAsync(null);

            // Act: add a playset and one additional mainboard card.
            await deckBuilder.AddPlaySetToDeckCommand.ExecuteAsync(new object[] { plains });

            await deckBuilder.AddCardToDeckCommand.ExecuteAsync(new object[] { prismaticEnding });

            // Act: add one sideboard and one maybeboard card.
            await deckBuilder.AddCardToSideboardCommand.ExecuteAsync(new object[] { leaveNoTrace });

            await deckBuilder.AddCardToMaybeboardCommand.ExecuteAsync(new object[] { deftbladeElite });

            // Assert: desired deck state in the VM.
            var commanderRow = Assert.Single(deckBuilder.CommanderZone.Cards);
            Assert.Equal("Sokrates, Athenian Teacher", commanderRow.CardName);
            Assert.Equal(1, commanderRow.DesiredQuantity);
            Assert.Equal(2, deckBuilder.MainboardZone.Cards.Count);
            var plainsRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Plains");

            Assert.Equal(4, plainsRow.DesiredQuantity);

            var prismaticEndingRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Prismatic Ending");

            Assert.Equal(1, prismaticEndingRow.DesiredQuantity);

            var sideboardRow = Assert.Single(deckBuilder.SideboardZone.Cards);
            Assert.Equal("Leave No Trace", sideboardRow.CardName);
            Assert.Equal(1, sideboardRow.DesiredQuantity);

            var maybeboardRow = Assert.Single(deckBuilder.MaybeboardZone.Cards);
            Assert.Equal("Deftblade Elite", maybeboardRow.CardName);
            Assert.Equal(1, maybeboardRow.DesiredQuantity);

            Assert.Empty(deckBuilder.CompanionZone.Cards);

            // Stats include Mainboard + Commander, but not Sideboard or Maybeboard.
            Assert.Equal(6, deckBuilder.Stats.CardCount);

            // Assert persisted desired deck state.
            var persistedDeckCards = await ScenarioTestHelpers.ExecuteQueryAsync<(string CardName, int Quantity, string Section)>(_ctx.DbFactory,
                """
                SELECT cardName, desiredQuantity, section
                FROM myDeckCards
                WHERE locationId = @locationId
                ORDER BY section, cardName COLLATE NOCASE;
                """,
                    reader => (
                        CardName: reader.GetString(reader.GetOrdinal("cardName")),
                        Quantity: reader.GetInt32(reader.GetOrdinal("desiredQuantity")),
                        Section: reader.GetString(reader.GetOrdinal("section"))
                    ),
                    cmd => cmd.Parameters.AddWithValue("@locationId", createdDeck.LocationId));

            Assert.Equal(5, persistedDeckCards.Count);

            Assert.Contains(persistedDeckCards, x => x.CardName == "Sokrates, Athenian Teacher" && x.Quantity == 1 && x.Section == "Commander");
            Assert.Contains(persistedDeckCards, x => x.CardName == "Plains" && x.Quantity == 4 && x.Section == "Mainboard");
            Assert.Contains(persistedDeckCards, x => x.CardName == "Prismatic Ending" && x.Quantity == 1 && x.Section == "Mainboard");
            Assert.Contains(persistedDeckCards, x => x.CardName == "Leave No Trace" && x.Quantity == 1 && x.Section == "Sideboard");
            Assert.Contains(persistedDeckCards, x => x.CardName == "Deftblade Elite" && x.Quantity == 1 && x.Section == "Maybeboard");

            // Act: leave Deck Builder through the normal VM navigation command.
            deckBuilder.BackToDeckManagementCommand.Execute(null);

            // Assert: scenario is back in Deck Management ready for Test 3.
            Assert.Same(_ctx.MainVM.DeckManagementVM, _ctx.MainVM.PagesDecksHostVM.CurrentDecksContentViewModel);

            #endregion

            #region Test 3 - Happy path update and clear format + string updates 

            // Act: update name, format and description for newly created deck

            // Assert initial state of deck editor for created deck
            Assert.Equal("Add a new deck", _ctx.MainVM.DeckManagementVM.ModeMessage);
            Assert.Equal("Add deck", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            _ctx.MainVM.DeckManagementVM.SelectedItem = createdDeck; // Select the created deck 

            // Assert deck editor state after selecting existing deck for edit
            Assert.Equal(string.Empty, _ctx.MainVM.DeckManagementVM.ModeMessage);
            Assert.Equal("Edit deck metadata", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null); // Click edit

            // ... then assert strings update
            Assert.Equal("Edit selected deck metadata", _ctx.MainVM.DeckManagementVM.ModeMessage);
            Assert.Equal("Save changes", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            _ctx.MainVM.DeckManagementVM.DeckName = "Control Pile";
            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = "casual";
            _ctx.MainVM.DeckManagementVM.Description = "Casual control pile";

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null); // Submit edit
            Assert.Equal("Add a new deck", _ctx.MainVM.DeckManagementVM.ModeMessage);
            Assert.Equal("Add deck", _ctx.MainVM.DeckManagementVM.ActionButtonText);
            Assert.Equal("Deck updated successfully.", _ctx.MainVM.DeckManagementVM.StatusMessage);

            // Assert deck manager state
            var updatedDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == createdDeck.LocationId);

            Assert.Equal("Control Pile", updatedDeck.Name);
            Assert.Equal("casual", updatedDeck.Format);
            Assert.Equal("Casual control pile", updatedDeck.Description);
            Assert.Equal("Casual/kitchen table", updatedDeck.FormatDisplayName);

            // Assert persisted update
            var updatedDeckRows = await ScenarioTestHelpers.ExecuteQueryAsync<
                (string Name, string Format, string Description)>(
                _ctx.DbFactory,
                """
                SELECT l.name, d.format, d.description
                FROM myDecks d
                INNER JOIN cardLocations l
                    ON l.id = d.locationId
                WHERE l.id = @id;
                """,
                reader => (
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    Format: reader.GetString(reader.GetOrdinal("format")),
                    Description: reader.GetString(reader.GetOrdinal("description"))
                ),
                cmd => cmd.Parameters.AddWithValue("@id", createdDeck.LocationId));

            var (UpdatedName, UpdatedFormat, UpdatedDescription) = Assert.Single(updatedDeckRows);

            Assert.Equal("Control Pile", UpdatedName);
            Assert.Equal("casual", UpdatedFormat);
            Assert.Equal("Casual control pile", UpdatedDescription);

            // Assert name is updated in modify collection viewmodel
            Assert.Contains(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations,
                x => x.Id == createdDeck.LocationId && x.DisplayName == "Deck: Control Pile");

            Assert.Contains(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations,
                x => x.Id == createdDeck.LocationId &&
                     x.DisplayName == "Deck: Control Pile");

            // Assert editor reloads canonical value when row is selected after update
            _ctx.MainVM.DeckManagementVM.SelectedItem = updatedDeck;

            Assert.Equal("Control Pile", _ctx.MainVM.DeckManagementVM.DeckName);
            Assert.Equal("casual", _ctx.MainVM.DeckManagementVM.SelectedDeckFormat);
            Assert.Equal("Casual control pile", _ctx.MainVM.DeckManagementVM.Description);

            // Assert filter option still exists after update and filtering is preserved after update
            var updatedLocationFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedLocationDisplayName"];
            Assert.Contains(updatedLocationFilter.FilterOptions, o => o.OptionName == "Deck: Control Pile");

            filteredCard = _ctx.MainVM.MyCollectionVM.FilteredCards.Single();

            Assert.Equal(updatedCard.CardId, filteredCard.CardId);
            Assert.Equal(createdLocation.Id, filteredCard.SelectedLocationId);
            Assert.Equal("Deck: Control Pile", filteredCard.SelectedLocationDisplayName);

            // Act: clear format through single edit
            Assert.Equal("Edit deck metadata", _ctx.MainVM.DeckManagementVM.ActionButtonText);
            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null); // Click edit
            Assert.Equal("Save changes", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = string.Empty;

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null); // Submit

            // Assert deck manager state after clearing format

            var clearedFormatDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == createdDeck.LocationId);

            Assert.True(string.IsNullOrWhiteSpace(clearedFormatDeck.Format));
            Assert.Equal("Casual control pile", clearedFormatDeck.Description);
            Assert.Equal(string.Empty, clearedFormatDeck.FormatDisplayName);

            // Assert persisted cleared format

            var clearedFormatRows = await ScenarioTestHelpers.ExecuteQueryAsync<(string? Format, string Description)>(
                _ctx.DbFactory,
                """
                SELECT format, description
                FROM myDecks d
                INNER JOIN cardLocations l
                    ON l.id = d.locationId
                WHERE l.id = @id;
                """,
                reader =>
                {
                    var formatOrdinal = reader.GetOrdinal("format");

                    return (
                        Format: reader.IsDBNull(formatOrdinal)
                            ? null
                            : reader.GetString(formatOrdinal),
                        Description: reader.GetString(reader.GetOrdinal("description"))
                    );
                },
                cmd => cmd.Parameters.AddWithValue("@id", createdDeck.LocationId));
            var (ClearedFormat, ClearedDescription) = Assert.Single(clearedFormatRows);

            Assert.True(string.IsNullOrWhiteSpace(ClearedFormat));
            Assert.Equal("Casual control pile", ClearedDescription);

            // Assert editor reloads blank format
            _ctx.MainVM.DeckManagementVM.SelectedItem = clearedFormatDeck;
            Assert.True(string.IsNullOrWhiteSpace(_ctx.MainVM.DeckManagementVM.SelectedDeckFormat));

            #endregion

            #region Test 4 - Check deckbuilder after metadata update
            _ctx.MainVM.DeckManagementVM.EnterDeckBuilderCommand.Execute(null);

            await StatusTestDriver.WaitUntilAsync(() => _ctx.MainVM.DeckBuilderVM.DeckName == "Control Pile" && _ctx.MainVM.DeckBuilderVM.MainboardZone.Cards.Count == 2, "Deck Builder did not finish reloading the updated deck.");

            // Assert: deck identity / format flowed from Deck Management into Deck Builder.
            Assert.Equal(createdDeck.LocationId, deckBuilder.DeckLocationId);
            Assert.Equal("Control Pile", deckBuilder.DeckName);
            Assert.Equal("", deckBuilder.DeckFormat);
            Assert.Equal("", deckBuilder.DeckFormatDisplayName);

            Assert.False(deckBuilder.IsCommanderZoneVisible); // format is no longer commander like format
            Assert.True(deckBuilder.IsSideboardZoneVisible);

            // Assert: previously persisted desired deck state was reloaded.
            commanderRow = Assert.Single(deckBuilder.CommanderZone.Cards);
            Assert.Equal("Sokrates, Athenian Teacher", commanderRow.CardName);
            Assert.Equal(1, commanderRow.DesiredQuantity);
            Assert.Equal(2, deckBuilder.MainboardZone.Cards.Count);

            plainsRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Plains"); Assert.Equal(4, plainsRow.DesiredQuantity);
            prismaticEndingRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Prismatic Ending");
            Assert.Equal(1, prismaticEndingRow.DesiredQuantity);

            sideboardRow = Assert.Single(deckBuilder.SideboardZone.Cards);
            Assert.Equal("Leave No Trace", sideboardRow.CardName);
            Assert.Equal(1, sideboardRow.DesiredQuantity);

            maybeboardRow = Assert.Single(deckBuilder.MaybeboardZone.Cards);
            Assert.Equal("Deftblade Elite", maybeboardRow.CardName);
            Assert.Equal(1, maybeboardRow.DesiredQuantity);

            Assert.Equal(6, deckBuilder.Stats.CardCount);

            #endregion

            #region Test 5 - Edit already persisted deck

            // Arrange: the deck from Test 4 is still loaded.
            // Existing state:
            // Mainboard: Plains x4, Prismatic Ending x1
            // Sideboard: Leave No Trace x1

            var deckRowsBeforeEdit = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myDeckCards
                WHERE locationId = @locationId;
                """,
                cmd => cmd.Parameters.AddWithValue("@locationId", createdDeck.LocationId));

            Assert.Equal(5, deckRowsBeforeEdit);

            // Act: increase quantities on already-persisted deck rows.
            await deckBuilder.AddCardToDeckCommand.ExecuteAsync(new object[] { plains });

            await deckBuilder.AddCardToSideboardCommand.ExecuteAsync(new object[] { leaveNoTrace });

            // Assert: existing VM rows were updated rather than duplicated.
            plainsRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Plains");

            Assert.Equal(5, plainsRow.DesiredQuantity);

            sideboardRow = deckBuilder.SideboardZone.Cards.Single(c => c.CardName == "Leave No Trace");

            Assert.Equal(2, sideboardRow.DesiredQuantity);

            Assert.Equal(2, deckBuilder.MainboardZone.Cards.Count);
            Assert.Single(deckBuilder.SideboardZone.Cards);

            // Mainboard increased by one.
            // Sideboard does not contribute to deck stats.
            Assert.Equal(7, deckBuilder.Stats.CardCount);

            // Assert: persistence updated the existing rows rather than inserting duplicates.
            var persistedEditedDeckCards = await ScenarioTestHelpers.ExecuteQueryAsync<(string CardName, int Quantity, string Section)>(_ctx.DbFactory,
                """
                SELECT cardName, desiredQuantity, section
                FROM myDeckCards
                WHERE locationId = @locationId
                ORDER BY section, cardName COLLATE NOCASE;
                """,
                reader => (CardName: reader.GetString(reader.GetOrdinal("cardName")), Quantity: reader.GetInt32(reader.GetOrdinal("desiredQuantity")), Section: reader.GetString(reader.GetOrdinal("section"))),
                cmd => cmd.Parameters.AddWithValue("@locationId", createdDeck.LocationId));

            Assert.Equal(5, persistedEditedDeckCards.Count);
            Assert.Contains(persistedEditedDeckCards, x => x.CardName == "Plains" && x.Quantity == 5 && x.Section == "Mainboard");
            Assert.Contains(persistedEditedDeckCards, x => x.CardName == "Leave No Trace" && x.Quantity == 2 && x.Section == "Sideboard");

            // Unchanged persisted rows are still present.
            Assert.Contains(persistedEditedDeckCards, x => x.CardName == "Prismatic Ending" && x.Quantity == 1 && x.Section == "Mainboard");
            Assert.Contains(persistedEditedDeckCards, x => x.CardName == "Sokrates, Athenian Teacher" && x.Quantity == 1 && x.Section == "Commander");
            Assert.Contains(persistedEditedDeckCards, x => x.CardName == "Deftblade Elite" && x.Quantity == 1 && x.Section == "Maybeboard");

            // Act: return to Deck Management for the next scenario step.
            deckBuilder.BackToDeckManagementCommand.Execute(null);

            Assert.Same(_ctx.MainVM.DeckManagementVM, _ctx.MainVM.PagesDecksHostVM.CurrentDecksContentViewModel);

            #endregion

            #region Test 6 - Add metadata to existing deck location

            // Arrange: Aggro Fish started as a deck location without metadata
            var aggroFish = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Aggro Fish");

            Assert.True(string.IsNullOrWhiteSpace(aggroFish.Format));
            Assert.True(string.IsNullOrWhiteSpace(aggroFish.Description));
            Assert.Equal(string.Empty, aggroFish.FormatDisplayName);

            // Act: add metadata to existing deck location
            _ctx.MainVM.DeckManagementVM.SelectedItem = aggroFish;

            Assert.Equal("Edit deck metadata", _ctx.MainVM.DeckManagementVM.ActionButtonText);
            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);
            Assert.Equal("Save changes", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = "modern";
            _ctx.MainVM.DeckManagementVM.Description = "Existing location upgraded to deck metadata";

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);

            // Assert deck manager state
            var updatedAggroFish = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == aggroFish.LocationId);

            Assert.Equal("Aggro Fish", updatedAggroFish.Name);
            Assert.Equal("modern", updatedAggroFish.Format);
            Assert.Equal("Existing location upgraded to deck metadata", updatedAggroFish.Description);
            Assert.Equal("Modern", updatedAggroFish.FormatDisplayName);
            Assert.Equal("Deck updated successfully.", _ctx.MainVM.DeckManagementVM.StatusMessage);

            // Assert persisted metadata was created
            var aggroFishRows = await ScenarioTestHelpers.ExecuteQueryAsync<(string Format, string Description)>(
                _ctx.DbFactory,
                """
                SELECT d.format, d.description
                FROM myDecks d
                WHERE d.locationId = @locationId;
                """,
                reader => (
                    Format: reader.GetString(reader.GetOrdinal("format")),
                    Description: reader.GetString(reader.GetOrdinal("description"))
                ),
                cmd => cmd.Parameters.AddWithValue("@locationId", aggroFish.LocationId));

            var (AggroFishFormat, AggroFishDescription) = Assert.Single(aggroFishRows);

            Assert.Equal("modern", AggroFishFormat);
            Assert.Equal("Existing location upgraded to deck metadata", AggroFishDescription);

            // Assert location manager still sees Aggro Fish as a deck location
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            var aggroFishLocation = _ctx.MainVM.CardLocationVM.Locations.Single(x => x.Id == aggroFish.LocationId);

            Assert.Equal("Aggro Fish", aggroFishLocation.Name);
            Assert.Equal(CardLocationType.Deck, aggroFishLocation.Type);

            #endregion

            #region Test 7 - Switch location type away from deck and back

            // Arrange: Control Pile currently exists as a deck with preserved metadata
            var controlPileDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == createdDeck.LocationId);

            Assert.Equal("Control Pile", controlPileDeck.Name);
            Assert.Equal("Casual control pile", controlPileDeck.Description);

            // Act: switch Control Pile from Deck to Storage in location manager
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            var controlPileLocation = _ctx.MainVM.CardLocationVM.Locations.Single(x => x.Id == controlPileDeck.LocationId);

            _ctx.MainVM.CardLocationVM.SelectedItem = controlPileLocation;
            await _ctx.MainVM.CardLocationVM.SubmitCommand.ExecuteAsync(null); // Click edit

            _ctx.MainVM.CardLocationVM.LocationName = "Control Pile";
            _ctx.MainVM.CardLocationVM.SelectedLocationType = CardLocationType.Storage;

            await _ctx.MainVM.CardLocationVM.SubmitCommand.ExecuteAsync(null); // Save

            // Assert location manager state
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            var storageControlPile = _ctx.MainVM.CardLocationVM.Locations.Single(x => x.Id == controlPileDeck.LocationId);

            Assert.Equal("Control Pile", storageControlPile.Name);
            Assert.Equal(CardLocationType.Storage, storageControlPile.Type);

            // Assert deck manager no longer shows Control Pile after reload
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();

            Assert.DoesNotContain(_ctx.MainVM.DeckManagementVM.Decks, x => x.LocationId == controlPileDeck.LocationId);

            // Assert: changing the location type away from Deck did not delete desired deck contents.
            var desiredDeckRowsWhileStorage =
                await ScenarioTestHelpers.ExecuteQueryAsync<(string CardName, int Quantity, string Section)>(_ctx.DbFactory,
                """
                SELECT cardName, desiredQuantity, section
                FROM myDeckCards
                WHERE locationId = @locationId
                ORDER BY section, cardName COLLATE NOCASE;
                """,
                reader => (CardName: reader.GetString(reader.GetOrdinal("cardName")), Quantity: reader.GetInt32(reader.GetOrdinal("desiredQuantity")), Section: reader.GetString(reader.GetOrdinal("section"))),
                cmd => cmd.Parameters.AddWithValue("@locationId", controlPileDeck.LocationId));

            Assert.Equal(5, desiredDeckRowsWhileStorage.Count);
            Assert.Contains(desiredDeckRowsWhileStorage, x => x.CardName == "Sokrates, Athenian Teacher" && x.Quantity == 1 && x.Section == "Commander");
            Assert.Contains(desiredDeckRowsWhileStorage, x => x.CardName == "Plains" && x.Quantity == 5 && x.Section == "Mainboard");
            Assert.Contains(desiredDeckRowsWhileStorage, x => x.CardName == "Prismatic Ending" && x.Quantity == 1 && x.Section == "Mainboard");
            Assert.Contains(desiredDeckRowsWhileStorage, x => x.CardName == "Leave No Trace" && x.Quantity == 2 && x.Section == "Sideboard");
            Assert.Contains(desiredDeckRowsWhileStorage, x => x.CardName == "Deftblade Elite" && x.Quantity == 1 && x.Section == "Maybeboard");

            // Assert deck metadata is preserved while location is Storage
            var preservedMetadataRows = await ScenarioTestHelpers.ExecuteQueryAsync<(string? Format, string Description)>(
                _ctx.DbFactory,
                """
                SELECT format, description
                FROM myDecks
                WHERE locationId = @locationId;
                """,
                reader =>
                {
                    var formatOrdinal = reader.GetOrdinal("format");

                    return (
                        Format: reader.IsDBNull(formatOrdinal)
                            ? null
                            : reader.GetString(formatOrdinal),
                        Description: reader.GetString(reader.GetOrdinal("description"))
                    );
                },
                cmd => cmd.Parameters.AddWithValue("@locationId", controlPileDeck.LocationId));
            var (PreservedFormat, PreservedDescription) = Assert.Single(preservedMetadataRows);

            Assert.True(string.IsNullOrWhiteSpace(PreservedFormat));
            Assert.Equal("Casual control pile", PreservedDescription);

            // Assert card using Control Pile still points to same location id
            var cardAfterStorageSwitch = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == updatedCard.CardId);

            Assert.Equal(controlPileDeck.LocationId, cardAfterStorageSwitch.SelectedLocationId);
            Assert.Equal("Storage: Control Pile", cardAfterStorageSwitch.SelectedLocationDisplayName);

            // Assert active location filter survived display-name/type change
            ScenarioTestHelpers.ApplyAllFilters(_ctx.MainVM, _ctx.FilteringService);

            var filteredAfterStorageSwitch = _ctx.MainVM.MyCollectionVM.FilteredCards.Single();

            Assert.Equal(updatedCard.CardId, filteredAfterStorageSwitch.CardId);
            Assert.Equal(controlPileDeck.LocationId, filteredAfterStorageSwitch.SelectedLocationId);
            Assert.Equal("Storage: Control Pile", filteredAfterStorageSwitch.SelectedLocationDisplayName);

            // Assert modify collection viewmodel location list reflects change
            Assert.Contains(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileDeck.LocationId && x.DisplayName == "Storage: Control Pile");
            Assert.Contains(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileDeck.LocationId && x.DisplayName == "Storage: Control Pile");

            // Act: switch Control Pile back from Storage to Deck
            var storageLocationForEdit = _ctx.MainVM.CardLocationVM.Locations.Single(x => x.Id == controlPileDeck.LocationId);

            _ctx.MainVM.CardLocationVM.SelectedItem = storageLocationForEdit;
            await _ctx.MainVM.CardLocationVM.SubmitCommand.ExecuteAsync(null); // Click edit

            _ctx.MainVM.CardLocationVM.LocationName = "Control Pile";
            _ctx.MainVM.CardLocationVM.SelectedLocationType = CardLocationType.Deck;

            await _ctx.MainVM.CardLocationVM.SubmitCommand.ExecuteAsync(null); // Save

            // Assert deck manager shows Control Pile again with preserved metadata
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();

            var restoredControlPileDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == controlPileDeck.LocationId);

            Assert.Equal("Control Pile", restoredControlPileDeck.Name);
            Assert.True(string.IsNullOrWhiteSpace(restoredControlPileDeck.Format));
            Assert.Equal("Casual control pile", restoredControlPileDeck.Description);
            Assert.Equal(string.Empty, restoredControlPileDeck.FormatDisplayName);

            // Assert card still points to same location and display name is back to Deck
            var cardAfterDeckSwitch = _ctx.MainVM.MyCollectionVM.Cards.Single(c => c.CardId == updatedCard.CardId);

            Assert.Equal(controlPileDeck.LocationId, cardAfterDeckSwitch.SelectedLocationId);
            Assert.Equal("Deck: Control Pile", cardAfterDeckSwitch.SelectedLocationDisplayName);

            // Assert active filter still returns the same card
            ScenarioTestHelpers.ApplyAllFilters(_ctx.MainVM, _ctx.FilteringService);

            var filteredAfterDeckSwitch = _ctx.MainVM.MyCollectionVM.FilteredCards.Single();

            Assert.Equal(updatedCard.CardId, filteredAfterDeckSwitch.CardId);
            Assert.Equal(controlPileDeck.LocationId, filteredAfterDeckSwitch.SelectedLocationId);
            Assert.Equal("Deck: Control Pile", filteredAfterDeckSwitch.SelectedLocationDisplayName);

            // Assert modify collection viewmodel location list reflects change
            Assert.Contains(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileDeck.LocationId && x.DisplayName == "Deck: Control Pile");
            Assert.Contains(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileDeck.LocationId && x.DisplayName == "Deck: Control Pile");

            // Act: reopen the restored deck in Deck Builder.
            _ctx.MainVM.DeckManagementVM.SelectedItem = restoredControlPileDeck;

            Assert.True(_ctx.MainVM.DeckManagementVM.IsEnterDeckBuilderButtonEnabled);

            _ctx.MainVM.DeckManagementVM.EnterDeckBuilderCommand.Execute(null);

            await StatusTestDriver.WaitUntilAsync(() => _ctx.MainVM.DeckBuilderVM.DeckName == "Control Pile" && _ctx.MainVM.DeckBuilderVM.MainboardZone.Cards.Any(c => c.CardName == "Plains" && c.DesiredQuantity == 5), "Deck Builder did not finish reloading Control Pile after restoring the location to Deck.");

            deckBuilder = _ctx.MainVM.DeckBuilderVM;

            commanderRow = Assert.Single(deckBuilder.CommanderZone.Cards);
            Assert.Equal("Sokrates, Athenian Teacher", commanderRow.CardName);
            Assert.Equal(1, commanderRow.DesiredQuantity);
            Assert.Equal(2, deckBuilder.MainboardZone.Cards.Count);

            plainsRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Plains"); Assert.Equal(5, plainsRow.DesiredQuantity);
            prismaticEndingRow = deckBuilder.MainboardZone.Cards.Single(c => c.CardName == "Prismatic Ending"); Assert.Equal(1, prismaticEndingRow.DesiredQuantity);
            sideboardRow = Assert.Single(deckBuilder.SideboardZone.Cards);

            Assert.Equal("Leave No Trace", sideboardRow.CardName);
            Assert.Equal(2, sideboardRow.DesiredQuantity);

            maybeboardRow = Assert.Single(deckBuilder.MaybeboardZone.Cards);
            Assert.Equal("Deftblade Elite", maybeboardRow.CardName);
            Assert.Equal(1, maybeboardRow.DesiredQuantity);

            Assert.Equal(7, deckBuilder.Stats.CardCount);

            deckBuilder.BackToDeckManagementCommand.Execute(null);

            Assert.Same(_ctx.MainVM.DeckManagementVM, _ctx.MainVM.PagesDecksHostVM.CurrentDecksContentViewModel);

            #endregion

            #region Test 8 - Multi-update deck formats

            // Arrange
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();

            var controlPile = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Control Pile");

            Assert.NotEqual(controlPile.LocationId, aggroFish.LocationId);

            // Verify blank bulk update is rejected
            _ctx.MainVM.DeckManagementVM.SelectedItems.Clear();
            _ctx.MainVM.DeckManagementVM.SelectedItems.Add(controlPile);
            _ctx.MainVM.DeckManagementVM.SelectedItems.Add(aggroFish);

            Assert.Equal("Update selected", _ctx.MainVM.DeckManagementVM.ActionButtonText);

            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = string.Empty;

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);
            Assert.Equal("Select a format before updating selected decks.", _ctx.MainVM.DeckManagementVM.StatusMessage);

            // Perform valid bulk update
            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = "casual";
            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);

            Assert.Equal("2 decks updated successfully.", _ctx.MainVM.DeckManagementVM.StatusMessage);

            // Assert deck manager state
            var updatedControlPile = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == controlPile.LocationId);
            updatedAggroFish = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.LocationId == aggroFish.LocationId);

            Assert.Equal("casual", updatedControlPile.Format);
            Assert.Equal("Casual/kitchen table", updatedControlPile.FormatDisplayName);

            Assert.Equal("casual", updatedAggroFish.Format);
            Assert.Equal("Casual/kitchen table", updatedAggroFish.FormatDisplayName);

            // Assert persisted state
            var persistedFormats = await ScenarioTestHelpers.ExecuteQueryAsync<(int LocationId, string Format)>(
                _ctx.DbFactory,
                """
                SELECT locationId, format
                FROM myDecks
                WHERE locationId IN (@id1, @id2)
                ORDER BY locationId;
                """,
                reader => (
                    LocationId: reader.GetInt32(reader.GetOrdinal("locationId")),
                    Format: reader.GetString(reader.GetOrdinal("format"))
                ),
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id1", controlPile.LocationId);
                    cmd.Parameters.AddWithValue("@id2", aggroFish.LocationId);
                });

            Assert.Equal(2, persistedFormats.Count);
            Assert.Contains(persistedFormats, x => x.LocationId == controlPile.LocationId && x.Format == "casual");
            Assert.Contains(persistedFormats, x => x.LocationId == aggroFish.LocationId && x.Format == "casual");

            // Assert selection cleared after successful bulk update
            Assert.Empty(_ctx.MainVM.DeckManagementVM.SelectedItems);
            Assert.Null(_ctx.MainVM.DeckManagementVM.SelectedItem);

            #endregion

            #region Test 9 - Delete single deck

            // Arrange
            await _ctx.MainVM.DeckManagementVM.LoadDecksAsync();
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            var aggroFishDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Aggro Fish");
            var aggroFishLocationId = aggroFishDeck.LocationId;

            Assert.Contains(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == aggroFishLocationId);

            // Act: delete Aggro Fish through deck manager
            _ctx.MainVM.DeckManagementVM.SelectedItem = aggroFishDeck;
            _ctx.MainVM.DeckManagementVM.SelectedItems.Clear();
            _ctx.MainVM.DeckManagementVM.SelectedItems.Add(aggroFishDeck);

            await _ctx.MainVM.DeckManagementVM.DeleteSelectedDecksCommand.ExecuteAsync(null); // confirm prompt
            Assert.Equal("Yes, delete!", _ctx.MainVM.DeckManagementVM.DeleteButtonText);

            await _ctx.MainVM.DeckManagementVM.DeleteSelectedDecksCommand.ExecuteAsync(null); // actual delete

            // Assert deck manager state
            Assert.DoesNotContain(_ctx.MainVM.DeckManagementVM.Decks, x => x.LocationId == aggroFishLocationId);

            // Assert location manager state
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            Assert.DoesNotContain(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == aggroFishLocationId);

            // Assert persisted location deleted
            var locationCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM cardLocations
                WHERE id = @id;
                """,
                cmd => cmd.Parameters.AddWithValue("@id", aggroFishLocationId));

            Assert.Equal(0, locationCount);

            // Assert persisted metadata deleted
            var metadataCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myDecks
                WHERE locationId = @id;
                """,
                cmd => cmd.Parameters.AddWithValue("@id", aggroFishLocationId));

            Assert.Equal(0, metadataCount);

            // Assert collection cards no longer point to deleted location
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.SelectedLocationId == aggroFishLocationId);

            var collectionReferenceCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myCollection
                WHERE locationId = @id;
                """,
                cmd => cmd.Parameters.AddWithValue("@id", aggroFishLocationId));

            Assert.Equal(0, collectionReferenceCount);

            // Assert location filter option removed
            var locationFilterAfterDelete = _ctx.MainVM.FilterPanelVM.Filters["SelectedLocationDisplayName"];

            Assert.DoesNotContain(locationFilterAfterDelete.FilterOptions, o => o.Value == aggroFishLocationId.ToString());
            Assert.DoesNotContain(locationFilterAfterDelete.FilterOptions, o => o.DisplayName == "Deck: Aggro Fish");

            // Assert modify collection editor location list updated
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == aggroFishLocationId);
            Assert.DoesNotContain(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == aggroFishLocationId);
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.DisplayName == "Deck: Aggro Fish");
            Assert.DoesNotContain(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.DisplayName == "Deck: Aggro Fish");

            #endregion

            #region Test 10 - Delete multiple decks

            // Arrange: create a temporary deck so bulk delete has two targets
            _ctx.MainVM.DeckManagementVM.DeckName = "Token Swarm";
            _ctx.MainVM.DeckManagementVM.SelectedDeckFormat = "standard";
            _ctx.MainVM.DeckManagementVM.Description = "Temporary token deck";

            await _ctx.MainVM.DeckManagementVM.SubmitCommand.ExecuteAsync(null);

            var tokenSwarmDeck = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Token Swarm");
            var controlPileDeckForDelete = _ctx.MainVM.DeckManagementVM.Decks.Single(x => x.Name == "Control Pile");

            var tokenSwarmLocationId = tokenSwarmDeck.LocationId;
            var controlPileLocationId = controlPileDeckForDelete.LocationId;

            // Assert: Control Pile still has its persisted desired deck before deletion.
            var desiredDeckRowsBeforeDelete = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myDeckCards
                WHERE locationId = @locationId;
                """,
                cmd => cmd.Parameters.AddWithValue("@locationId", controlPileLocationId));

            Assert.Equal(5, desiredDeckRowsBeforeDelete);

            // Refresh location manager so both locations are visible there too
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            Assert.Contains(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == tokenSwarmLocationId);
            Assert.Contains(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == controlPileLocationId);

            // Assign Token Swarm to one unassigned collection card
            var tokenCard = _ctx.MainVM.MyCollectionVM.Cards.First(c => c.SelectedLocationId is null);
            var setTokenLocationParam = new SetLocationForSelectedCardsParameter(new object[] { tokenCard }, tokenSwarmLocationId);

            _ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(setTokenLocationParam);

            // Assert both decks are referenced by collection before delete
            Assert.Contains(_ctx.MainVM.MyCollectionVM.Cards, c => c.SelectedLocationId == tokenSwarmLocationId);
            Assert.Contains(_ctx.MainVM.MyCollectionVM.Cards, c => c.SelectedLocationId == controlPileLocationId);

            // Act: multi-delete Token Swarm and Control Pile
            _ctx.MainVM.DeckManagementVM.SelectedItem = null;
            _ctx.MainVM.DeckManagementVM.SelectedItems.Clear();
            _ctx.MainVM.DeckManagementVM.SelectedItems.Add(tokenSwarmDeck);
            _ctx.MainVM.DeckManagementVM.SelectedItems.Add(controlPileDeckForDelete);

            await _ctx.MainVM.DeckManagementVM.DeleteSelectedDecksCommand.ExecuteAsync(null); // confirm prompt
            Assert.Equal("Yes, delete!", _ctx.MainVM.DeckManagementVM.DeleteButtonText);

            await _ctx.MainVM.DeckManagementVM.DeleteSelectedDecksCommand.ExecuteAsync(null); // actual delete

            // Assert deck manager state
            Assert.DoesNotContain(_ctx.MainVM.DeckManagementVM.Decks, x => x.LocationId == tokenSwarmLocationId);
            Assert.DoesNotContain(_ctx.MainVM.DeckManagementVM.Decks, x => x.LocationId == controlPileLocationId);

            // Assert location manager state
            await _ctx.MainVM.CardLocationVM.LoadCardLocationsAsync();

            Assert.DoesNotContain(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == tokenSwarmLocationId);
            Assert.DoesNotContain(_ctx.MainVM.CardLocationVM.Locations, x => x.Id == controlPileLocationId);

            // Assert available location lists updated
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == tokenSwarmLocationId);
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileLocationId);
            Assert.DoesNotContain(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == tokenSwarmLocationId);
            Assert.DoesNotContain(_ctx.MainVM.SearchAndFilterPageVM.ModifyCollectionViewModel!.AvailableLocations, x => x.Id == controlPileLocationId);

            // Assert persisted locations deleted
            var deletedLocationCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM cardLocations
                WHERE id IN (@id1, @id2);
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id1", tokenSwarmLocationId);
                    cmd.Parameters.AddWithValue("@id2", controlPileLocationId);
                });

            Assert.Equal(0, deletedLocationCount);

            // Assert persisted metadata deleted
            var deletedMetadataCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(
                _ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myDecks
                WHERE locationId IN (@id1, @id2);
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id1", tokenSwarmLocationId);
                    cmd.Parameters.AddWithValue("@id2", controlPileLocationId);
                });

            Assert.Equal(0, deletedMetadataCount);

            // Assert desired deck contents were deleted with Control Pile.
            var deletedDesiredDeckRows = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myDeckCards
                WHERE locationId = @locationId;
                """,
                cmd => cmd.Parameters.AddWithValue("@locationId", controlPileLocationId));

            Assert.Equal(0, deletedDesiredDeckRows);

            // Assert collection cards no longer point to deleted locations
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.SelectedLocationId == tokenSwarmLocationId);
            Assert.DoesNotContain(_ctx.MainVM.MyCollectionVM.Cards, c => c.SelectedLocationId == controlPileLocationId);

            var deletedCollectionReferenceCount = await ScenarioTestHelpers.ExecuteScalarAsync<int>(_ctx.DbFactory,
                """
                SELECT COUNT(*)
                FROM myCollection
                WHERE locationId IN (@id1, @id2);
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id1", tokenSwarmLocationId);
                    cmd.Parameters.AddWithValue("@id2", controlPileLocationId);
                });

            Assert.Equal(0, deletedCollectionReferenceCount);

            // Assert location filter options removed
            var locationFilterAfterBulkDelete = _ctx.MainVM.FilterPanelVM.Filters["SelectedLocationDisplayName"];

            Assert.DoesNotContain(locationFilterAfterBulkDelete.FilterOptions, o => o.Value == tokenSwarmLocationId.ToString());
            Assert.DoesNotContain(locationFilterAfterBulkDelete.FilterOptions, o => o.Value == controlPileLocationId.ToString());
            Assert.DoesNotContain(locationFilterAfterBulkDelete.FilterOptions, o => o.DisplayName == "Deck: Token Swarm");
            Assert.DoesNotContain(locationFilterAfterBulkDelete.FilterOptions, o => o.DisplayName == "Deck: Control Pile");

            #endregion
        }
    }
}

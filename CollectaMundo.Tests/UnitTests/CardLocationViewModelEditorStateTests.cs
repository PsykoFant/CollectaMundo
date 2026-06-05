using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.ViewModels.Utilities;
using Moq;

namespace CollectaMundo.Tests.UnitTests
{
    public class CardLocationViewModelEditorStateTests
    {
        [Fact]
        public void SelectingSingleLocation_EntersSelectedReadOnlyMode()
        {
            var vm = CreateViewModel();
            var location = CreateLocation();

            vm.SelectedItem = location;

            Assert.Equal("Binder", vm.LocationName);
            Assert.Equal(CardLocationType.Storage, vm.SelectedLocationType);

            Assert.False(vm.IsSingleItemTextEditorEnabled);
            Assert.False(vm.IsDiscreteValueEditorEnabled);

            Assert.Equal("Edit location", vm.ActionButtonText);
            Assert.True(vm.IsActionButtonEnabled);

            Assert.True(vm.HasSelectedItems == false);
            Assert.False(vm.IsCancelVisible);
            Assert.Equal(string.Empty, vm.ModeMessage);
        }

        [Fact]
        public void ClearSelectionFromPreview_RestoresCreateMode()
        {
            var vm = CreateViewModel();
            var location = CreateLocation();

            vm.SelectedItem = location;

            vm.ClearSelectionAndRestoreCreateModeCommand.Execute(null);

            Assert.Null(vm.SelectedItem);
            Assert.Empty(vm.SelectedItems);
            Assert.Equal(string.Empty, vm.LocationName);
            Assert.Equal(CardLocationType.Storage, vm.SelectedLocationType);

            Assert.Equal("Add location", vm.ActionButtonText);
            Assert.False(vm.IsCancelVisible);
        }

        [Fact]
        public void SubmitFromSelectedReadOnlyMode_EntersEditSingleMode()
        {
            var vm = CreateViewModel();
            var location = CreateLocation();

            vm.SelectedItem = location;

            vm.SubmitCommand.Execute(null);

            Assert.True(vm.IsSingleItemTextEditorEnabled);
            Assert.True(vm.IsDiscreteValueEditorEnabled);

            Assert.Equal("Save changes", vm.ActionButtonText);
            Assert.True(vm.IsActionButtonEnabled);
            Assert.True(vm.IsCancelVisible);
            Assert.Equal("Edit selected card location", vm.ModeMessage);
        }

        [Fact]
        public async Task DeleteSelectedLocation_FirstClick_EntersDeleteConfirmation()
        {
            var vm = CreateViewModel();
            var location = CreateLocation();

            vm.SelectedItem = location;
            vm.SelectedItems.Add(location);

            await vm.DeleteSelectedLocationsCommand.ExecuteAsync(null);

            Assert.Equal("Yes, delete!", vm.DeleteButtonText);
            Assert.False(vm.IsActionButtonEnabled);
            Assert.True(vm.IsCancelVisible);
            Assert.True(vm.IsStatusVisible);
            Assert.Contains("delete", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CancelEdit_DuringDeleteConfirmation_ClearsConfirmationAndSelection()
        {
            var vm = CreateViewModel();
            var location = CreateLocation();

            vm.SelectedItem = location;
            vm.SelectedItems.Add(location);

            await vm.DeleteSelectedLocationsCommand.ExecuteAsync(null);

            vm.CancelEditCommand.Execute(null);

            Assert.Equal("Delete selected", vm.DeleteButtonText);
            Assert.True(vm.IsActionButtonEnabled);
            Assert.False(vm.IsCancelVisible);
            Assert.False(vm.IsStatusVisible);
            Assert.Equal(string.Empty, vm.StatusMessage);

            Assert.Null(vm.SelectedItem);
            Assert.Empty(vm.SelectedItems);
            Assert.Equal("Add location", vm.ActionButtonText);
        }

        [Fact]
        public void SelectingMultipleLocations_EntersEditMultipleMode()
        {
            var vm = CreateViewModel();
            var first = CreateLocation(1, "Binder");
            var second = CreateLocation(2, "Box", CardLocationType.Deck);

            vm.SelectedItems.Add(first);
            vm.SelectedItems.Add(second);

            Assert.Equal(string.Empty, vm.LocationName);
            Assert.Null(vm.SelectedLocationType);

            Assert.False(vm.IsSingleItemTextEditorEnabled);
            Assert.True(vm.IsDiscreteValueEditorEnabled);

            Assert.Equal("Update selected", vm.ActionButtonText);
            Assert.True(vm.IsActionButtonEnabled);
            Assert.True(vm.IsCancelVisible);
            Assert.Equal("Edit selected card locations", vm.ModeMessage);
        }

        [Fact]
        public async Task SubmitFromMultiEdit_CallsUpdateLocationTypesAsync()
        {
            var serviceMock = new Mock<ICardLocationService>();

            var first = CreateLocation(1, "Binder");
            var second = CreateLocation(2, "Box", CardLocationType.Deck);

            serviceMock
                .Setup(s => s.UpdateLocationTypesAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    CardLocationType.Deck,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var vm = new CardLocationViewModel(serviceMock.Object);

            vm.SelectedItems.Add(first);
            vm.SelectedItems.Add(second);
            vm.SelectedLocationType = CardLocationType.Deck;

            await vm.SubmitCommand.ExecuteAsync(null);

            serviceMock.Verify(
                s => s.UpdateLocationTypesAsync(
                    It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1, 2 })),
                    CardLocationType.Deck,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SuccessfulMultiEdit_UpdatesLocationsAndResetsSelection()
        {
            var serviceMock = new Mock<ICardLocationService>();
            var first = CreateLocation(1, "Binder");
            var second = CreateLocation(2, "Box", CardLocationType.Deck);

            serviceMock
                .Setup(s => s.UpdateLocationTypesAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    CardLocationType.Deck,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    CreateLocation(1, "Binder", CardLocationType.Deck),
                    CreateLocation(2, "Box", CardLocationType.Deck)
                    ]);

            var vm = new CardLocationViewModel(serviceMock.Object);

            vm.Locations.Add(first);
            vm.Locations.Add(second);

            vm.SelectedItems.Add(first);
            vm.SelectedItems.Add(second);
            vm.SelectedLocationType = CardLocationType.Deck;

            await vm.SubmitCommand.ExecuteAsync(null);

            Assert.Empty(vm.SelectedItems);
            Assert.Null(vm.SelectedItem);
            Assert.Equal("Add location", vm.ActionButtonText);

            Assert.All(vm.Locations, location =>
                Assert.Equal(CardLocationType.Deck, location.Type));

            Assert.True(vm.IsStatusVisible);
            Assert.Equal("2 locations updated successfully.", vm.StatusMessage);
        }

        [Fact]
        public async Task DeleteSelectedLocations_WithMultipleSelected_EntersDeleteConfirmation()
        {
            var vm = CreateViewModel();

            vm.SelectedItems.Add(CreateLocation(1, "Binder"));
            vm.SelectedItems.Add(CreateLocation(2, "Box"));

            await vm.DeleteSelectedLocationsCommand.ExecuteAsync(null);

            Assert.Equal("Yes, delete!", vm.DeleteButtonText);
            Assert.False(vm.IsActionButtonEnabled);
            Assert.True(vm.IsCancelVisible);
            Assert.True(vm.IsStatusVisible);
        }

        private static CardLocationViewModel CreateViewModel(Mock<ICardLocationService>? serviceMock = null)
        {
            serviceMock ??= new Mock<ICardLocationService>();

            return new CardLocationViewModel(serviceMock.Object);
        }

        private static CardLocation CreateLocation(int id = 1, string name = "Binder", CardLocationType type = CardLocationType.Storage)
        {
            return new CardLocation
            {
                Id = id,
                Name = name,
                Type = type
            };
        }
    }
}

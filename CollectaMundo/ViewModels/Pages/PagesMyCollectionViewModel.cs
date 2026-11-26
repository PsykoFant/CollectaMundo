using CollectaMundo.ApplicationServices.CollectionExport;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.CardPrices.ViewModels;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.ModifyCollection;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.Shell.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed partial class PagesMyCollectionViewModel(
        CardListViewModel<CollectionCard> cardsVM,
        FilterPanelViewModel filterVM,
        string pageTitle,
        ShellPageEnum cardListPage,
        ICollectionExportService collectionExportService,
        string primarySubmitButtonText,
        ICommand? primarySubmitCommand = null,
        PricesViewModel? pricesVM = null,
        ModifyCollectionViewModel? modifyCollectionVM = null)
        : CardListPageViewModel<CollectionCard>(cardsVM, filterVM, pageTitle, cardListPage, primarySubmitButtonText, primarySubmitCommand, pricesVM, modifyCollectionVM)
    {
        private readonly ICollectionExportService _collectionExportService = collectionExportService;

        [ObservableProperty]
        private string exportStatusMessage = string.Empty;

        [RelayCommand]
        private async Task ExportFilteredTradeListAsync(CancellationToken cancellationToken)
        {
            var collectionCardIds = CardsVM.FilteredCards.Select(card => card.CardId).ToHashSet();

            ExportStatusMessage = "Exporting filtered trade list…";

            try
            {
                var result = await _collectionExportService.ExportFilteredTradeListAsync(collectionCardIds, cancellationToken);

                ExportStatusMessage = result.Code == OperationResultCode.Success
                    ? $"Filtered trade list exported to {result.Message}"
                    : result.Message;
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"Export failed: {ex.Message}";
            }
        }

        public override void ClearPageStatus()
        {
            base.ClearPageStatus();
            ExportStatusMessage = string.Empty;
        }
    }
}

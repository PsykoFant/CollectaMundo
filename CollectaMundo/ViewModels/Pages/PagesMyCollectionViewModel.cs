using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.SideMenuRight;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class PagesMyCollectionViewModel(
        CardViewModel cardsVM,
        CardImageViewModel cardImageVM,
        FilterViewModel filterVM,
        string pageTitle,
        string primarySubmitButtonText,
        ICommand? primarySubmitCommand = null,
        PricesViewModel? pricesVM = null,
        ModifyCollectionViewModel? modifyCollectionVM = null) : CardListPageViewModel(cardsVM, cardImageVM, filterVM, pageTitle, primarySubmitButtonText, primarySubmitCommand, pricesVM, modifyCollectionVM)
    {
        // All the logic is in CardListPageViewModel, this class just serves to differentiate the page type for the view
    }
}

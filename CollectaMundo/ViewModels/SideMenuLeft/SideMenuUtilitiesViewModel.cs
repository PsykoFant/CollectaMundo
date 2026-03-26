using CollectaMundo.ViewModels.Utilities;

namespace CollectaMundo.ViewModels.SideMenuLeft
{

    public sealed class SideMenuUtilitiesViewModel(UtilitiesViewModel utilitiesVM, PricesViewModel pricesVM)
    {
        public UtilitiesViewModel UtilitiesVM { get; } = utilitiesVM;
        public PricesViewModel PricesVM { get; } = pricesVM;
    }
}


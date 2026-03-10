namespace CollectaMundo.ViewModels.SideMenuLeft
{

    public sealed class UtilitiesSideMenuViewModel(UtilitiesViewModel utilitiesVM, PricesViewModel pricesVM)
    {
        public UtilitiesViewModel UtilitiesVM { get; } = utilitiesVM;
        public PricesViewModel PricesVM { get; } = pricesVM;
    }
}


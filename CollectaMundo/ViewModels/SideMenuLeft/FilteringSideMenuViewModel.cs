namespace CollectaMundo.ViewModels.SideMenuLeft
{
    public sealed class FilteringSideMenuViewModel(FilterViewModel filterVM, CardViewModel colorIconsViewModel)
    {
        public FilterViewModel FilterVM { get; } = filterVM;
        public CardViewModel ColorIconsViewModel { get; } = colorIconsViewModel;
    }
}


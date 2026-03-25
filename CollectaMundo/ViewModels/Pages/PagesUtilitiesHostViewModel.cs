using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Pages;
using CollectaMundo.ViewModels.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesUtilitiesHostViewModel : ObservableObject, IUtilitiesHostController
    {
        public UtilitiesHomeViewModel UtilitiesHomeVM { get; }
        public ImportViewModel ImportVM { get; }

        [ObservableProperty]
        private object currentUtilitiesContentViewModel;

        public PagesUtilitiesHostViewModel(
            UtilitiesHomeViewModel utilitiesHomeVM,
            ImportViewModel importVM)
        {
            UtilitiesHomeVM = utilitiesHomeVM;
            ImportVM = importVM;
            currentUtilitiesContentViewModel = UtilitiesHomeVM;
        }

        public void ShowHome() => CurrentUtilitiesContentViewModel = UtilitiesHomeVM;

        public void ShowImport() => CurrentUtilitiesContentViewModel = ImportVM;

        public async Task ShowImportAsync()
        {
            CurrentUtilitiesContentViewModel = ImportVM;
            await ImportVM.Begin();
        }

        public void ShowUtilitiesHome()
        {
            ImportVM.EndImport();
            CurrentUtilitiesContentViewModel = UtilitiesHomeVM;
        }
    }
}

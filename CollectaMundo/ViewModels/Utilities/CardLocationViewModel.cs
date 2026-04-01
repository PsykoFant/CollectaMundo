using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CardLocationViewModel : ObservableObject
    {
        [ObservableProperty]
        private string pageTitle = "Manage Locations";
    }
}

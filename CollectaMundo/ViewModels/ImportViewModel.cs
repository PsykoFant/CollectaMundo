using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        [ObservableProperty]
        private Visibility importOverlayVisibility = Visibility.Collapsed;

        // More steps will be tracked here later (e.g. enums, state info)
    }
}

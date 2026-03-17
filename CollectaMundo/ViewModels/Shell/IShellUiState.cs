using System.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellUiState : INotifyPropertyChanged
    {
        bool IsSideMenuLeftVisible { get; set; }
        bool IsTopMenuEnabled { get; set; }

        Visibility CardViewSectionVisibility { get; set; }

        public void SetUiBusy(bool isBusy);

        object? CurrentPageViewModel { get; set; }
        object? CurrentSideMenuViewModel { get; set; }
    }
}

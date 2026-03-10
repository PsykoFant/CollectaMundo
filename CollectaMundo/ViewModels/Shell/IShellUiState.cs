using System.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellUiState : INotifyPropertyChanged
    {
        bool IsSideMenuLeftVisible { get; set; }
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }
        public void SetUiBusy(bool isBusy);


        object? CurrentPageViewModel { get; set; }
        object? CurrentSideMenuViewModel { get; set; }
    }
}

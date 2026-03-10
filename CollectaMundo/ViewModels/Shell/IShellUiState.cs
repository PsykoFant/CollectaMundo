using System.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellUiState : INotifyPropertyChanged
    {
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }
        public void SetUiBusy(bool isBusy);
        object? CurrentPageViewModel { get; set; }
    }
}

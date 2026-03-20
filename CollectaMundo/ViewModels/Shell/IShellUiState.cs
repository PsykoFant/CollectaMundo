using System.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellUiState : INotifyPropertyChanged
    {
        bool IsSideMenuLeftVisible { get; set; }
        bool IsSideMenuRightVisible { get; set; }
        bool IsTopMenuEnabled { get; set; }
        void SetUiBusy(bool isBusy);
        ShellPage CurrentPage { get; set; }
    }
}

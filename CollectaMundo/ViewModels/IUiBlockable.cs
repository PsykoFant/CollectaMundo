using System.ComponentModel;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public interface IUiBlockable : INotifyPropertyChanged
    {
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }
    }

}

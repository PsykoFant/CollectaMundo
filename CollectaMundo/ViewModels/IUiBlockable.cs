using System.Windows;

namespace CollectaMundo.ViewModels
{
    public interface IUiBlockable
    {
        Visibility SideMenuVisibility { get; set; }
        Visibility CardViewSectionVisibility { get; set; }
        bool IsTopMenuEnabled { get; set; }
    }

}

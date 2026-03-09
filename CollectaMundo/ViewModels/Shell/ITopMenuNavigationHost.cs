using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ViewModels.Shell
{
    public interface ITopMenuNavigationHost : INotifyPropertyChanged
    {
        object? CurrentPageViewModel { get; set; }
        bool IsTopMenuEnabled { get; }
    }
}

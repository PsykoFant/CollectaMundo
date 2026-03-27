using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public interface INavigationCleanupService
    {
        void CleanupBeforePageChange(object? oldPageViewModel, object? newPageViewModel);
    }
}

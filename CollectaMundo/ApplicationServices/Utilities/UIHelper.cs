using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class UIHelper
    {
        public static async Task ForceRenderAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Yield(); // Ensures control returns to dispatcher
        }
    }
}

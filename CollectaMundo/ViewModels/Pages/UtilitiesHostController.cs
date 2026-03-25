using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class UtilitiesHostController(PagesUtilitiesHostViewModel vm)
    : IUtilitiesHostController
    {
        private readonly PagesUtilitiesHostViewModel _vm = vm;
        public Task ShowImportAsync() => _vm.ShowImportAsync();
        public void ShowUtilitiesHome() => _vm.ShowHome();
    }
}

using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.Shared
{
    public sealed class ImportOverlayController(ImportViewModel vm) : IImportOverlayController
    {
        private readonly ImportViewModel _vm = vm;
        public async void ShowImportOverlayAndBeginImport()
        {
            await _vm.Begin();
        }
        public void HideImportOverlayAndEndImport()
        {
            _vm.EndImport();
        }
    }
}

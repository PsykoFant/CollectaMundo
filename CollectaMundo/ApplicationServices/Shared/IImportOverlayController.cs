using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IImportOverlayController
    {
        Task ShowImportOverlayAndBeginImport();
        void HideImportOverlayAndEndImport();
    }
}

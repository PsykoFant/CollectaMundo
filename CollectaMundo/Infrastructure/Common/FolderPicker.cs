using CollectaMundo.ApplicationServices.Shared;
using Ookii.Dialogs.Wpf;
using System.Windows;

namespace CollectaMundo.Infrastructure.Common
{
    public class FolderPicker : IFolderPicker
    {
        public string? PickFolder(string title, string? initialPath = null)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                SelectedPath = initialPath ?? string.Empty,
                ShowNewFolderButton = true
            };

            var owner = Application.Current?.MainWindow;
            bool? result = dialog.ShowDialog(owner);

            return result == true ? dialog.SelectedPath : null;
        }
    }
}

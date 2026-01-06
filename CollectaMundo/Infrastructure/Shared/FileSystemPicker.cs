using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Windows;

namespace CollectaMundo.Infrastructure.Shared
{
    public class FileSystemPicker : IFileSystemPicker
    {
        public string? PickFile(string title, string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*")
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false
            };

            var result = dialog.ShowDialog(Application.Current?.MainWindow);
            return result == true ? dialog.FileName : null;
        }
        public string? PickFolder(string title, string? initialPath = null)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                SelectedPath = initialPath ?? string.Empty,
                ShowNewFolderButton = true
            };

            var result = dialog.ShowDialog(Application.Current?.MainWindow);
            return result == true ? dialog.SelectedPath : null;
        }
        public string? PickSaveFile(string title, string defaultFileName, string filter)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                Filter = filter,
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            var result = dialog.ShowDialog(Application.Current?.MainWindow);
            return result == true ? dialog.FileName : null;
        }
    }
}

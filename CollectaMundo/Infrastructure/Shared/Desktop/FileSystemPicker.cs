using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo.Infrastructure.Shared.Desktop
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
        public string? PickSaveFile(string title, string defaultFileName, string filter, string defaultExtension)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                FileName = MakeValidFileName(defaultFileName),
                Filter = filter,
                DefaultExt = defaultExtension,
                AddExtension = true,
                OverwritePrompt = true
            };

            var result = dialog.ShowDialog(Application.Current?.MainWindow);
            return result == true ? dialog.FileName : null;
        }
        private static string MakeValidFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string([.. fileName.Select(c => invalidChars.Contains(c) ? '-' : c)]);

            return sanitized.Trim();
        }
        public void OpenFolder(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(fullPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(fullPath);

            Process.Start(startInfo);
        }
    }
}


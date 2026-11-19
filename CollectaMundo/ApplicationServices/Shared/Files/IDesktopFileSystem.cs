namespace CollectaMundo.ApplicationServices.Shared.Files
{
    public interface IDesktopFileSystem
    {
        string? PickFile(string title, string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
        string? PickFolder(string title, string? initialPath = null);
        string? PickSaveFile(string title, string defaultFileName, string filter, string defaultExtension);
        void OpenFolder(string path);
    }
}

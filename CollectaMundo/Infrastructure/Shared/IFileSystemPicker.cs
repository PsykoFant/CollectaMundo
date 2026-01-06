namespace CollectaMundo.Infrastructure.Shared
{
    public interface IFileSystemPicker
    {
        string? PickFile(string title, string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
        string? PickFolder(string title, string? initialPath = null);
        string? PickSaveFile(string title, string defaultFileName, string filter);
    }

}

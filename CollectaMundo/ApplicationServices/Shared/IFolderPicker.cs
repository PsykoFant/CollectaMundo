namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IFolderPicker
    {
        string? PickFolder(string title, string? initialPath = null);
    }
}

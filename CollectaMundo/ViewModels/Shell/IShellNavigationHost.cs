namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellNavigationHost : IShellUiState
    {
        object? CurrentPageViewModel { get; set; }
        object? CurrentSideMenuViewModel { get; set; }
    }
}

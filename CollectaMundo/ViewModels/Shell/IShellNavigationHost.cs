namespace CollectaMundo.ViewModels.Shell
{
    public interface IShellNavigationHost : IShellUiState
    {
        object? CurrentPageViewModel { get; set; }
        object? CurrentSideMenuLeftViewModel { get; set; }
        object? CurrentSideMenuRightViewModel { get; set; }
    }
}

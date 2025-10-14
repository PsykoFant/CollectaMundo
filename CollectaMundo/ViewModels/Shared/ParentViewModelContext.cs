namespace CollectaMundo.ViewModels.Shared
{
    public class ParentViewModelContext(IUiBlockable uiState, IAppRefresher appRefresher)
    {
        public IUiBlockable UiState { get; } = uiState;
        public IAppRefresher AppRefresher { get; } = appRefresher;
    }
}


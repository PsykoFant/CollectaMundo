namespace CollectaMundo.ViewModels
{
    public interface IAppRefresher
    {
        Task ReloadAllCardListsAndFiltersAsync();
    }
}

using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public interface IFilterInitDefaultsService
    {
        Task InitializeFiltersAsync(Dictionary<string, FilterItemViewModel> target, FilterViewModel viewModel);
    }
}

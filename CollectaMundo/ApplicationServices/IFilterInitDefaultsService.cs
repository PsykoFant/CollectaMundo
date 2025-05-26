using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface IFilterInitDefaultsService
    {
        Task InitializeFiltersAsync(Dictionary<string, FilterItemViewModel> target, FilterViewModel viewModel);
    }
}

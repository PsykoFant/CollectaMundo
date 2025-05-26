using CollectaMundo.Data;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public class FilterInitDefaultsService(IUnitOfWork uow) : IFilterInitDefaultsService
    {
        private readonly IUnitOfWork _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        public async Task InitializeFiltersAsync(Dictionary<string, FilterItemViewModel> target, FilterViewModel viewModel)
        {
            await _uow.BeginAsync();
            try
            {

                var repo = new FilterInitDefaultsRepository(_uow.CurrentConnection);
                var defaults = await repo.GetFilterDefaultsAsync();

                target.Clear(); // reset if needed
                foreach (var def in defaults)
                {
                    target[def.CriteriaKey] = new FilterItemViewModel(
                        def.CriteriaKey,
                        def.FilterOptions,
                        def.DefaultText,
                        def.ReadableLabel,
                        viewModel,
                        def.NumericCriteria);
                }

                await _uow.CommitAsync();
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                await _uow.DisposeAsync();
            }
        }

    }
}

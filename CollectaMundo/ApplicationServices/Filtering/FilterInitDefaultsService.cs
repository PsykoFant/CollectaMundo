using CollectaMundo.Data;
using CollectaMundo.Data.Filtering;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public class FilterInitDefaultsService(IDbConnectionFactory dbFactory) : IFilterInitDefaultsService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        public async Task InitializeFiltersAsync(Dictionary<string, FilterItemViewModel> target, FilterViewModel viewModel)
        {

            await using var uow = new UnitOfWork(_dbFactory);

            try
            {

                var repo = new FilterInitDefaultsRepository();
                var defaults = await repo.GetFilterDefaultsAsync(uow.CurrentConnection);

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

                await uow.CommitAsync();
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                Debug.WriteLine($"Error initializing filters: {ex.Message}");
            }
            finally
            {
                await uow.DisposeAsync();
            }
        }
    }
}

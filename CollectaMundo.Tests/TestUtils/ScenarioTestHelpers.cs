using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.ViewModels;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Tests.TestUtils
{
    public static class ScenarioTestHelpers
    {
        public static void ApplyAllFilters(MainWindowViewModel mainVM, FilteringService filteringService)
        {
            mainVM.AllCardsVM.FilteredCards = filteringService.ApplyFilters(mainVM.AllCardsVM.Cards, mainVM.FilterPanelVM.Filters.Values);
            mainVM.MyCollectionVM.FilteredCards = filteringService.ApplyFilters(mainVM.MyCollectionVM.Cards, mainVM.FilterPanelVM.Filters.Values);
        }
        public static void AssertFiltersCleared(MainWindowViewModel mainVM)
        {
            Assert.Equal(65, mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(22, mainVM.MyCollectionVM.FilteredCards.Count);
            Assert.True(string.IsNullOrEmpty(mainVM.FilterPanelVM.FilterSummary));
        }
        public static PrintingCard FindCard(IEnumerable<PrintingCard> source, string uuid)
        {
            return source.Single(c => string.Equals(c.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        }

        public static CollectionCard FindCard(IEnumerable<CollectionCard> source, string uuid)
        {
            return source.Single(c => string.Equals(c.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        }
        public static async Task<T> ExecuteScalarAsync<T>(IDbConnectionFactory dbFactory, string sql, Action<SQLiteCommand>? configure = null)
        {
            var uowRunner = new UnitOfWorkRunner(dbFactory);

            return await uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                using var cmd = new SQLiteCommand(sql, conn);

                configure?.Invoke(cmd);

                object? result = await cmd.ExecuteScalarAsync();

                return result is null or DBNull
                    ? default!
                    : (T)Convert.ChangeType(result, typeof(T));
            });
        }
        public static async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(IDbConnectionFactory dbFactory, string sql, Func<DbDataReader, T> map, Action<SQLiteCommand>? configure = null)
        {
            var uowRunner = new UnitOfWorkRunner(dbFactory);

            return await uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                using var cmd = new SQLiteCommand(sql, conn);

                configure?.Invoke(cmd);

                var results = new List<T>();

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(map(reader));
                }

                return results;
            });
        }
    }
}

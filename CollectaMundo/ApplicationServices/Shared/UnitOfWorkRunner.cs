using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.Shared
{

    public sealed class UnitOfWorkRunner(IDbConnectionFactory dbFactory) : IUnitOfWorkRunner
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        public async Task<T> ExecuteWriteAsync<T>(Func<SQLiteConnection, SQLiteTransaction, Task<(T Result, bool Commit)>> action)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var outcome = await action(
                    uow.CurrentConnection,
                    uow.CurrentTransaction);

                if (outcome.Commit)
                {
                    await uow.CommitAsync();
                }
                else
                {
                    await uow.RollbackAsync();
                }

                return outcome.Result;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
        public async Task<T> ExecuteReadOnlyAsync<T>(Func<SQLiteConnection, Task<T>> action)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            return await action(uow.CurrentConnection);
        }
    }
}

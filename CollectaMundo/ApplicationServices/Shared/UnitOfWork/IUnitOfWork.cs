using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.Shared.UnitOfWork
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task BeginAsync();
        Task BeginReadOnlyAsync();
        Task CommitAsync();
        Task RollbackAsync();

        // When you start a UoW, repositories can grab this transaction and pass it into their SQLiteCommand constructors.
        SQLiteTransaction CurrentTransaction { get; }
        SQLiteConnection CurrentConnection { get; }
    }
}


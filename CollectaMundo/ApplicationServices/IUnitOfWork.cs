using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task BeginAsync();
        Task BeginReadOnlyAsync();
        Task CommitAsync();
        Task RollbackAsync();

        /// <summary>
        /// When you start a UoW, repositories can grab this transaction and
        /// pass it into their SQLiteCommand constructors.
        /// </summary>
        SQLiteTransaction CurrentTransaction { get; }
        SQLiteConnection CurrentConnection { get; }
    }
}


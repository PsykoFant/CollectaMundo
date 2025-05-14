namespace CollectaMundo.ApplicationServices
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        // Opens the connection (if not already open) and begins a DB transaction.
        Task BeginAsync();

        // Commits the current transaction.
        Task CommitAsync();

        // Rolls back the current transaction.
        Task RollbackAsync();
    }
}


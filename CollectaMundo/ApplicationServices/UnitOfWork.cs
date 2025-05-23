using CollectaMundo.Data;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public class UnitOfWork(IDbConnectionFactory dbFactory) : IUnitOfWork
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        private SQLiteTransaction? _txn;

        public async Task BeginAsync()
        {
            // 1) Open (or re-use) the single shared connection
            var conn = await _dbFactory.OpenConnectionAsync();

            // 2) Start a transaction on it
            _txn = conn.BeginTransaction();
        }

        public Task CommitAsync()
        {
            _txn?.Commit();
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            _txn?.Rollback();
            return Task.CompletedTask;
        }

        public SQLiteTransaction CurrentTransaction => _txn ?? throw new InvalidOperationException("You must call BeginAsync() before using the transaction.");

        public ValueTask DisposeAsync()
        {
            // tear down—close the shared connection when you're fully done
            _dbFactory.CloseConnection();
            return ValueTask.CompletedTask;
        }
    }
}

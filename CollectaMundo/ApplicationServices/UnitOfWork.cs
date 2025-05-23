using CollectaMundo.Data;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public class UnitOfWork(IDbConnectionFactory dbFactory) : IUnitOfWork
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        private SQLiteConnection? _conn;
        private SQLiteTransaction? _txn;

        public async Task BeginAsync()
        {
            _conn ??= await _dbFactory.OpenConnectionAsync(); // reuse if already opened
            _txn = _conn.BeginTransaction();
        }

        public SQLiteConnection CurrentConnection => _conn ?? throw new InvalidOperationException("BeginAsync must be called first.");
        public SQLiteTransaction CurrentTransaction => _txn ?? throw new InvalidOperationException("BeginAsync must be called first.");

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

        public ValueTask DisposeAsync()
        {
            _txn?.Dispose();
            _txn = null;

            _conn?.Dispose();
            _conn = null;

            return ValueTask.CompletedTask;
        }
    }

}

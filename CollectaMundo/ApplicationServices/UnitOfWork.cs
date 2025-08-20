using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices
{
    public class UnitOfWork : IUnitOfWork
    {
        private SQLiteConnection? _conn;
        private SQLiteTransaction? _txn;

        public async Task BeginAsync()
        {
            if (AppGlobals.DbFactory is null)
            {
                throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
            }

            _conn ??= await AppGlobals.DbFactory.OpenConnectionAsync(); // reuse if already opened
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

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_txn != null)
                {
                    await _txn.DisposeAsync();
                    _txn = null;
                }

                if (_conn != null)
                {
                    var conn = _conn;
                    _conn = null; // clear reference early
                    await conn.CloseAsync();
                    await conn.DisposeAsync();
                    Debug.WriteLine($"[DISPOSE] Reader/Command disposed");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UnitOfWork] DisposeAsync error: {ex.Message}");
            }
        }
    }
}

using CollectaMundo.Infrastructure.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class UnitOfWork(IDbConnectionFactory dbFactory) : IUnitOfWork
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private SQLiteConnection? _conn;
        private SQLiteTransaction? _txn;

        // Begin a normal (write-capable) UoW: opens a connection and starts a transaction.
        public async Task BeginAsync()
        {
            if (_dbFactory is null)
            {
                throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
            }

            if (_conn is null)
            {
                _conn = await _dbFactory.OpenConnectionAsync();
                ApplyCommonPragmas(_conn, readOnly: false);
            }

            _txn = _conn.BeginTransaction();
        }

        // Begin a read-only UoW: opens a connection without a transaction and enables PRAGMA query_only.
        // Use this for large SELECT-only startup loads to avoid writer/reader contention.
        public async Task BeginReadOnlyAsync()
        {
            if (_dbFactory is null)
            {
                throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
            }

            if (_conn is null)
            {
                _conn = await _dbFactory.OpenConnectionAsync();
                ApplyCommonPragmas(_conn, readOnly: true);
            }

            // IMPORTANT: no transaction for read-only work
            _txn = null;
        }

        public SQLiteConnection CurrentConnection => _conn ?? throw new InvalidOperationException("BeginAsync/BeginReadOnlyAsync must be called first.");
        public SQLiteTransaction CurrentTransaction => _txn ?? throw new InvalidOperationException("No active transaction. Call BeginAsync() for write operations.");
        public Task CommitAsync()
        {
            _txn?.Commit(); // no-op if read-only path
            return Task.CompletedTask;
        }
        public Task RollbackAsync()
        {
            _txn?.Rollback(); // no-op if read-only path
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
                    Debug.WriteLine("[DISPOSE] Reader/Command disposed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UnitOfWork] DisposeAsync error: {ex.Message}");
            }
        }
        private static void ApplyCommonPragmas(SQLiteConnection conn, bool readOnly)
        {
            using var cmd = conn.CreateCommand();
            // Avoid changing journal_mode here; do that once elsewhere if needed.
            // query_only=ON guards against writes on this connection.
            cmd.CommandText = readOnly
                ? "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL; PRAGMA query_only=ON;"
                : "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL; PRAGMA query_only=OFF;";
            cmd.ExecuteNonQuery();
        }
    }
}

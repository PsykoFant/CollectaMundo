using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices
{
    public class UnitOfWork : IUnitOfWork
    {
        private SQLiteConnection? _conn;
        private SQLiteTransaction? _txn;

        /// <summary>
        /// Begin a normal (write-capable) UoW: opens a connection and starts a transaction.
        /// </summary>
        public async Task BeginAsync()
        {
            if (AppGlobals.DbFactory is null)
            {
                throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
            }

            if (_conn is null)
            {
                _conn = await AppGlobals.DbFactory.OpenConnectionAsync(); // <-- your existing factory
                ApplyCommonPragmas(_conn, readOnly: false);
            }

            _txn = _conn.BeginTransaction();
        }

        /// <summary>
        /// Begin a read-only UoW: opens a connection without a transaction and enables PRAGMA query_only.
        /// Use this for large SELECT-only startup loads to avoid writer/reader contention.
        /// </summary>
        public async Task BeginReadOnlyAsync()
        {
            if (AppGlobals.DbFactory is null)
            {
                throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
            }

            if (_conn is null)
            {
                _conn = await AppGlobals.DbFactory.OpenConnectionAsync(); // <-- same factory
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

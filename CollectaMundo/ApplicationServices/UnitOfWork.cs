using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public class UnitOfWork : IUnitOfWork
    {
        private SQLiteTransaction? _txn;

        public async Task BeginAsync()
        {
            // 1) ensure the connection is open
            await DBAccess.OpenConnectionAsync();

            // 2) grab the connection and null‐check it
            var conn = DBAccess.connection
                ?? throw new InvalidOperationException("DBAccess.connection was null");

            // 3) start the transaction on the connection
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

        public ValueTask DisposeAsync()
        {
            // always close the connection, even if already closed
            DBAccess.CloseConnection();
            return ValueTask.CompletedTask;
        }
    }

}

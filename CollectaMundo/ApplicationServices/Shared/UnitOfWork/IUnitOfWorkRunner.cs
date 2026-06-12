using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.Shared.UnitOfWork
{
    public interface IUnitOfWorkRunner
    {
        Task<T> ExecuteWriteAsync<T>(Func<SQLiteConnection, SQLiteTransaction, Task<(T Result, bool Commit)>> action);
        Task<T> ExecuteReadOnlyAsync<T>(Func<SQLiteConnection, Task<T>> action);
    }
}


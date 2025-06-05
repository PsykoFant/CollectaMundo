namespace CollectaMundo.ApplicationServices
{
    public interface IDatabaseIntegrityService
    {
        Task<DatabaseStatus> GetDatabaseStatusAsync();
    }
    public enum DatabaseStatus
    {
        Missing,
        Corrupt,
        Healthy
    }
}

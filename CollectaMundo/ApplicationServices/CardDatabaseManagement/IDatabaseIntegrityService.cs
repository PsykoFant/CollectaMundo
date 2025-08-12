namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
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

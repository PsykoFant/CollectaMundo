namespace CollectaMundo.ApplicationServices
{
    public interface IStartupService
    {
        Task EnsureDatabaseIntegrityAsync();
    }
}

using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface IDatabaseIntegrityService
    {
        Task<DatabaseStatus> GetDatabaseStatusAsync();
    }
}

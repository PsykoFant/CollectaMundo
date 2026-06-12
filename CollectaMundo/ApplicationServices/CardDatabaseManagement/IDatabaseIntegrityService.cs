using CollectaMundo.ApplicationServices.CardDatabaseManagement.Models;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface IDatabaseIntegrityService
    {
        Task<DatabaseStatus> GetDatabaseStatusAsync();
    }
}

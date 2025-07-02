namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public interface IUpdateService
    {
        Task CheckForDbUpdatesAsync();
    }
}

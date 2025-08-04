namespace CollectaMundo.ApplicationServices.Utilities.InternetCheck
{
    public interface IInternetConnectivityService
    {
        Task<bool> IsInternetAvailableAsync();
    }

}

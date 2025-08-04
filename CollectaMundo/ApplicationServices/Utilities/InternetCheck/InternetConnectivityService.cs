using System.Net.Http;

namespace CollectaMundo.ApplicationServices.Utilities.InternetCheck
{
    public class InternetConnectivityService : IInternetConnectivityService
    {
        public async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var result = await client.GetAsync("https://www.google.com");
                return result.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

}

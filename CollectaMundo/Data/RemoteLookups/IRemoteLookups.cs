using Newtonsoft.Json.Linq;

namespace CollectaMundo.Data.RemoteLookups
{
    public interface IRemoteLookups
    {
        Task<bool> IsInternetAvailableAsync();
        Task<JArray?> FetchSetMetadataAsync();
        string? TryGetIconUriForSetCode(JArray setMetadata, string setCode);
        Task<string?> FetchSvgContentAsync(string svgUrl);
        Task<int> FetchSetsCountAsync();
    }
}

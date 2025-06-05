using Newtonsoft.Json.Linq;

namespace CollectaMundo.Data.ScryfallLookups
{
    public interface IScryfallLookups
    {
        Task<JArray?> FetchSetMetadataAsync();
        string? TryGetIconUriForSetCode(JArray setMetadata, string setCode);
        Task<string?> FetchSvgContentAsync(string svgUrl);
    }
}

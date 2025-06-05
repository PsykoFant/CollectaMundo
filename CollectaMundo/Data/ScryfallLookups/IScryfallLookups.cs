using Newtonsoft.Json.Linq;

namespace CollectaMundo.Data.ScryfallLookups
{
    public interface IScryfallLookups
    {
        Task<JArray?> FetchSetMetadataAsync();
    }
}

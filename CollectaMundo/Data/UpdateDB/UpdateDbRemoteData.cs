using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;

namespace CollectaMundo.Data.UpdateDB
{
    public class UpdateDbRemoteData : IUpdateDbRemoteData
    {
        public async Task<int> FetchSetsCountAsync()
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync("https://mtgjson.com/api/v5/SetList.json");
            var json = JObject.Parse(response);
            var sets = json["data"] as JArray;
            int count = sets?.Count ?? 0;
            Debug.WriteLine($"Number of sets fetched: {count}");
            return count;
        }
    }
}


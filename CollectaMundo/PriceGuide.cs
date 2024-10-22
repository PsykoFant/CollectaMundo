using Newtonsoft.Json;

namespace CollectaMundo
{
    public class PriceGuide
    {
        public int IdProduct { get; set; }
        public decimal? Avg { get; set; }
        public decimal? Low { get; set; }
        public decimal? Trend { get; set; }
        public decimal? Avg1 { get; set; }
        public decimal? Avg7 { get; set; }
        public decimal? Avg30 { get; set; }

        [JsonProperty("avg-foil")]
        public decimal? AvgFoil { get; set; }

        [JsonProperty("low-foil")]
        public decimal? LowFoil { get; set; }

        [JsonProperty("trend-foil")]
        public decimal? TrendFoil { get; set; }

        [JsonProperty("avg1-foil")]
        public decimal? Avg1Foil { get; set; }

        [JsonProperty("avg7-foil")]
        public decimal? Avg7Foil { get; set; }

        [JsonProperty("avg30-foil")]
        public decimal? Avg30Foil { get; set; }
    }
}

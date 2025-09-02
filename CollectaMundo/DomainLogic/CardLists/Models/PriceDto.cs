namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class PriceDto
    {
        public string Uuid { get; init; } = "";
        public decimal? NormalPrice { get; init; }
        public decimal? FoilPrice { get; init; }
        public decimal? EtchedPrice { get; init; }

    }
}

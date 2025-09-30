namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class SetDto
    {
        public string Code { get; init; } = "";
        public string TokenCode { get; init; } = "";
        public string Name { get; init; } = "";
        public DateTime? ReleaseDate { get; init; }
    }
}

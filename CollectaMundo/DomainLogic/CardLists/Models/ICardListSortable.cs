namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public interface ICardListSortable
    {
        DateTime? ReleaseDate { get; }
        string? SetCode { get; }
        string? Colors { get; }
        string? Types { get; }
        int GamePlayCard { get; } 
    }
}

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilteringLogic<TCard>
    {
        bool Matches(TCard card);
    }
}

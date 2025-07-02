namespace CollectaMundo.Data.UpdateDB
{
    public interface IUpdateDbRemoteData
    {
        Task<int> FetchSetsCountAsync();
    }
}

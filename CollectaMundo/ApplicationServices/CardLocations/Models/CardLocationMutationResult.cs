namespace CollectaMundo.ApplicationServices.CardLocations.Models
{
    using CollectaMundo.ApplicationServices.Shared;
    using CollectaMundo.DomainLogic.CardLocations.Models;

    public sealed record CardLocationMutationResult(OperationResult Result, CardLocation? Location);
}

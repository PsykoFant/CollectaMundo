using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.ApplicationServices.CardLocations.Models
{
    public sealed record MutationResult<T>(OperationResult Result, T? Entity);
}

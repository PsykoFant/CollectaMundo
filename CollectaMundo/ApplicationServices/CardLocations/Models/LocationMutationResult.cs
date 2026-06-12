using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CardLocations.Models
{
    public sealed record MutationResult<T>(OperationResult Result, T? Entity);
}

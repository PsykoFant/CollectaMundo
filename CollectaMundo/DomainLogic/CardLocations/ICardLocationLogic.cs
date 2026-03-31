using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.DomainLogic.CardLocations
{
    public interface ICardLocationLogic
    {
        string NormalizeName(string? name);
        OperationResult ValidateForCreate(string? name, CardLocationType type);
        OperationResult ValidateForUpdate(int id, string? name, CardLocationType type);
        OperationResult ValidateId(int id);
    }
}

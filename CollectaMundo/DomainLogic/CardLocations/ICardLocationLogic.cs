using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.DomainLogic.CardLocations
{
    public interface ICardLocationLogic
    {
        string NormalizeName(string? name);
        OperationResult ValidateNameAndType(string? name, CardLocationType type);
    }
}

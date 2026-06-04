using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.DomainLogic.CardLocations
{
    public sealed class CardLocationLogic : ICardLocationLogic
    {
        private const int MaxNameLength = 100;
        public string NormalizeName(string? name)
        {
            return (name ?? string.Empty).Trim();
        }
        public OperationResult ValidateNameAndType(string? name, CardLocationType type)
        {
            string normalizedName = NormalizeName(name);

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return new OperationResult(OperationResultCode.ValidationFailed, "Location name is required.");
            }

            if (normalizedName.Length > MaxNameLength)
            {
                return new OperationResult(OperationResultCode.ValidationFailed, $"Location name cannot exceed {MaxNameLength} characters.");
            }

            if (!Enum.IsDefined(type))
            {
                return new OperationResult(OperationResultCode.ValidationFailed, "Location type is invalid.");
            }

            return new OperationResult(OperationResultCode.Success);
        }
    }
}

using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class CollectionIdentityFactory
    {
        public static CollectionIdentity Create(string? uuid, string? condition, string? language, string? finish, int? locationId, string? comment)
        {
            return new CollectionIdentity(
                uuid ?? throw new InvalidOperationException("Uuid required"),
                condition ?? throw new InvalidOperationException("Condition required"),
                language ?? throw new InvalidOperationException("Language required"),
                finish ?? throw new InvalidOperationException("Finish required"),
                locationId,
                NormalizeComment(comment));
        }
        private static string? NormalizeComment(string? comment)
        {
            var trimmed = comment?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }
    }
}

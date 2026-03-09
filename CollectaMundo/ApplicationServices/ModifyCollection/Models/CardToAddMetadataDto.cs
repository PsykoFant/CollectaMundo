namespace CollectaMundo.ApplicationServices.EditCollection.Models
{
    public sealed class CardToAddMetadataDto
    {
        public IReadOnlyList<string> AvailableFinishes { get; init; } = [];
        public IReadOnlyList<string> AvailableLanguages { get; init; } = [];
    }
}

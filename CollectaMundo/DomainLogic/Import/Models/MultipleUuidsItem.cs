using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class MultipleUuidsItem : ObservableObject
    {
        public required string Name { get; init; }
        public required string CMImportKey { get; init; }
        public required List<UuidVersion> VersionedUuids { get; init; }

        [ObservableProperty]
        private string? selectedUuid;
    }
}

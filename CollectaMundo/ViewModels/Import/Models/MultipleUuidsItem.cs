using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Import.Models
{
    public partial class MultipleUuidsItem : ObservableObject
    {
        public required string Name { get; init; }
        public required string TempItemImportKey { get; init; }
        public required List<UuidVersion> VersionedUuids { get; init; }

        [ObservableProperty]
        private string? selectedUuid;

        // Callback when selection changes
        public Action<string>? OnSelectionChangedCallback { get; set; }
        partial void OnSelectedUuidChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                OnSelectionChangedCallback?.Invoke(value);
            }
        }
    }
}

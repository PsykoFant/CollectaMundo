namespace CollectaMundo.Models
{
    public class FilterSelections
    {
        public String? SelectedName { get; set; } = null;
        public String? SelectedSetName { get; set; } = null;

        public HashSet<string> SelectedColors { get; set; } = [];
        public HashSet<string> SelectedTypes { get; set; } = [];
        public HashSet<string> SelectedSuperTypes { get; set; } = [];
        public HashSet<string> SelectedSubTypes { get; set; } = [];
        public HashSet<string> SelectedKeywords { get; set; } = [];
        public HashSet<string> SelectedFinishes { get; set; } = [];
        public HashSet<string> SelectedLanguages { get; set; } = [];
        public HashSet<string> SelectedConditions { get; set; } = [];
        public HashSet<string> SelectedRarity { get; set; } = [];
    }
}

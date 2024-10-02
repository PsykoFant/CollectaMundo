namespace CollectaMundo
{
    public class FilterContext
    {
        public List<string> AllColors { get; set; } = [];
        public List<string> AllTypes { get; set; } = [];
        public List<string> AllSuperTypes { get; set; } = [];
        public List<string> AllSubTypes { get; set; } = [];
        public List<string> AllKeywords { get; set; } = [];
        public List<string> CardNames { get; set; } = [];

        public HashSet<string> SelectedColors { get; set; } = [];
        public HashSet<string> SelectedTypes { get; set; } = [];
        public HashSet<string> SelectedSuperTypes { get; set; } = [];
        public HashSet<string> SelectedSubTypes { get; set; } = [];
        public HashSet<string> SelectedKeywords { get; set; } = [];

        // Filter defaults
        public string RulesTextDefaultText { get; } = "Filter rulestext...";
        public string TypesDefaultText { get; } = "Filter card types...";
        public string SuperTypesDefaultText { get; } = "Filter supertypes...";
        public string SubTypesDefaultText { get; } = "Filter subtypes...";
        public string KeywordsDefaultText { get; } = "Filter keywords...";

    }

}

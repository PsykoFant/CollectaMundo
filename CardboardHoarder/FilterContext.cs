namespace CardboardHoarder
{
    public class FilterContext
    {
        public List<string> AllColors { get; set; } = new List<string>();
        public List<string> AllTypes { get; set; } = new List<string>();
        public List<string> AllSuperTypes { get; set; } = new List<string>();
        public List<string> AllSubTypes { get; set; } = new List<string>();
        public List<string> AllKeywords { get; set; } = new List<string>();

        public HashSet<string> SelectedColors { get; set; } = new HashSet<string>();
        public HashSet<string> SelectedTypes { get; set; } = new HashSet<string>();
        public HashSet<string> SelectedSuperTypes { get; set; } = new HashSet<string>();
        public HashSet<string> SelectedSubTypes { get; set; } = new HashSet<string>();
        public HashSet<string> SelectedKeywords { get; set; } = new HashSet<string>();

        public string RulesTextDefaultText { get; } = "Filter rulestext...";
        public string TypesDefaultText { get; } = "Filter card types...";
        public string SuperTypesDefaultText { get; } = "Filter supertypes...";
        public string SubTypesDefaultText { get; } = "Filter subtypes...";
        public string KeywordsDefaultText { get; } = "Filter keywords...";
    }

}

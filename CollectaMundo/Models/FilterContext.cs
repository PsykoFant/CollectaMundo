namespace CollectaMundo.Models
{
    public class FilterContext
    {
        public List<string> AllColors { get; set; } = [];
        public List<string> AllTypes { get; set; } = [];
        public List<string> AllSuperTypes { get; set; } = [];
        public List<string> AllSubTypes { get; set; } = [];
        public List<string> AllKeywords { get; set; } = [];
        public List<string> AllFinishes { get; set; } = [];
        public List<string> AllLanguages { get; set; } = [];
        public List<string> AllConditions { get; set; } = [];
        public HashSet<string> SelectedColors { get; set; } = [];
        public HashSet<string> SelectedTypes { get; set; } = [];
        public HashSet<string> SelectedSuperTypes { get; set; } = [];
        public HashSet<string> SelectedSubTypes { get; set; } = [];
        public HashSet<string> SelectedKeywords { get; set; } = [];
        public HashSet<string> SelectedFinishes { get; set; } = [];
        public HashSet<string> SelectedLanguages { get; set; } = [];
        public HashSet<string> SelectedConditions { get; set; } = [];

        // Filter defaults
        public string RulesTextDefaultText { get; } = "Filter rulestext...";
        public string TypesDefaultText { get; } = "Filter card types...";
        public string SuperTypesDefaultText { get; } = "Filter supertypes...";
        public string SubTypesDefaultText { get; } = "Filter subtypes...";
        public string KeywordsDefaultText { get; } = "Filter keywords...";
        public string FinishesDefaultText { get; } = "Filter card finishes...";
        public string LanguagesDefaultText { get; } = "Filter card languages...";
        public string ConditionsDefaultText { get; } = "Filter card conditions...";
    }
}

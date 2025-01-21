using System.Windows.Controls;

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
        public List<string> AllRarities { get; set; } = [];
        public TextBox? TextBox { get; set; }
        public ListBox? ListBox { get; set; }

        // Filter defaults
        public string RulesTextDefaultText { get; } = "Filter rulestext ...";
        public string TypesDefaultText { get; } = "Filter card types ...";
        public string SuperTypesDefaultText { get; } = "Filter supertypes ...";
        public string SubTypesDefaultText { get; } = "Filter subtypes ...";
        public string KeywordsDefaultText { get; } = "Filter keywords ...";
        public string FinishesDefaultText { get; } = "Filter finishes ...";
        public string LanguagesDefaultText { get; } = "Filter languages ...";
        public string ConditionsDefaultText { get; } = "Filter conditions ...";
        public string RarityDefaultText { get; } = "Filter rarity ...";
    }
}

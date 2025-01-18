using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class FilterSelections
    {

        public String? CriteriaKey { get; set; } = null;
        public OperatorType Operator { get; set; } = 0;
        public String? SingleCriteria { get; set; } = null;
        public HashSet<string> MultipleCriteria { get; set; } = [];



        //public String? SelectedName { get; set; } = null;
        //public String? SelectedSetName { get; set; } = null;

        //public HashSet<string> SelectedColors { get; set; } = [];
        //public HashSet<string> SelectedTypes { get; set; } = [];
        //public HashSet<string> SelectedSuperTypes { get; set; } = [];
        //public HashSet<string> SelectedSubTypes { get; set; } = [];
        //public HashSet<string> SelectedKeywords { get; set; } = [];
        //public HashSet<string> SelectedFinishes { get; set; } = [];
        //public HashSet<string> SelectedLanguages { get; set; } = [];
        //public HashSet<string> SelectedConditions { get; set; } = [];
        //public HashSet<string> SelectedRarity { get; set; } = [];
        //public int ColorOperator { get; set; } = 0;
        //public int TypesOperator { get; set; } = 0;
        //public int SuperTypesOperator { get; set; } = 0;
        //public int SubTypesOperator { get; set; } = 0;
        //public int KeywordsOperator { get; set; } = 0;
        //public int FinishesOperator { get; set; } = 0;
        //public int LanguagesOperator { get; set; } = 0;
        //public int ConditionsOperator { get; set; } = 0;
        //public int RarityOperator { get; set; } = 0;
    }
}

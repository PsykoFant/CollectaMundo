using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class Filters
    {
        public String? CriteriaKey { get; set; } = null;
    }
    public class FilterSelections : Filters
    {
        public OperatorType Operator { get; set; } = 0;
        public String? SingleCriteria { get; set; } = null;
        public HashSet<string> MultipleCriteria { get; set; } = [];
    }
    public class FilterDefaults : Filters
    {
        public List<string> AllCriteria { get; set; } = [];
        public string? DefaultText { get; set; } = null;
    }
}

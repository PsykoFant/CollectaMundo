namespace CollectaMundo.DomainLogic.Import.Models
{
    public class ImportMatchSummaryDto
    {
        public int TotalItems { get; set; }
        public int ItemsWithUuid { get; set; }
        public int ItemsWithMultipleUuids { get; set; }
    }

}

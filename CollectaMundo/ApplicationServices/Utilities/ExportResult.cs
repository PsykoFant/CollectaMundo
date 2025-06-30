namespace CollectaMundo.ApplicationServices.Utilities
{

    public record ExportResult(ExportResultCode Code, string Message);
    public enum ExportResultCode
    {
        Success = 0,
        Error = 1,
        Empty = 2
    }

}

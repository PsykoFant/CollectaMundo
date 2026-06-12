namespace CollectaMundo.ApplicationServices.Shared.Operation
{
    public record OperationResult(OperationResultCode Code, string Message = "");
    public enum OperationResultCode
    {
        Success = 0,
        Error = 1,
        Empty = 2,
        UpToDate = 3,
        NeedsUpdate = 4,
        DownloadFailed = 5,
        NoInternet = 6,
        CancelledByUser = 7,
        NoOp = 8,
        ValidationFailed = 9,
        AlreadyExists = 10,
        NotFound = 11
    }
}

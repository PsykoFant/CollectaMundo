namespace CollectaMundo.ApplicationServices.Utilities.Downloads
{
    public interface ICardDatabaseDownloadCoordinator
    {
        Task<bool> DownloadWithRetryAsync(
            string dbPath,
            string pricesPath,
            IProgress<string>? stepLabelProgress = null,
            IProgress<string>? detailProgress = null,
            IProgress<int>? percentProgress = null
        );
    }
}

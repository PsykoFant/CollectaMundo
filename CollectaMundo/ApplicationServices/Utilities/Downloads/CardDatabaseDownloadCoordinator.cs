namespace CollectaMundo.ApplicationServices.Utilities.Downloads
{
    public class CardDatabaseDownloadCoordinator(IAppSettings settings) : ICardDatabaseDownloadCoordinator
    {
        private readonly IAppSettings _settings = settings;

        public async Task<bool> DownloadWithRetryAsync(
            string dbPath,
            string pricesPath,
            IProgress<string>? stepLabelProgress = null,
            IProgress<string>? detailProgress = null,
            IProgress<int>? percentProgress = null)
        {
            return await RetryHelper.RetryLoopAsync(
                async () =>
                {
                    using var innerCts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token);
                    var token = linkedCts.Token;

                    var taskA = DownloadResourceHelper.DownloadResourceAsync(
                        _settings.CardDatabaseUrl,
                        dbPath,
                        taskLabel: "Card database",
                        statusProgress: detailProgress,
                        percentProgress: percentProgress,
                        token: token);

                    var taskB = DownloadResourceHelper.DownloadResourceAsync(
                        _settings.CardPricesUrl,
                        pricesPath,
                        taskLabel: "Price file",
                        statusProgress: null,
                        percentProgress: null,
                        token: token);

                    var firstCompleted = await Task.WhenAny(taskA, taskB);
                    var firstResult = await firstCompleted;

                    if (!firstResult.success)
                    {
                        innerCts.Cancel();
                        await Task.WhenAll(taskA, taskB);
                        if (!string.IsNullOrWhiteSpace(firstResult.errorMessage))
                            throw new Exception(firstResult.errorMessage);
                        return false;
                    }

                    var finalA = await taskA;
                    var finalB = await taskB;

                    if (!finalA.success || !finalB.success)
                    {
                        string error = finalA.errorMessage ?? finalB.errorMessage ?? "Unknown download error.";
                        throw new Exception(error);
                    }

                    return true;
                },
                stepName: "Step 1. Downloading resource files...",
                maxRetries: 3,
                stepNameProgress: stepLabelProgress,
                detailProgress: detailProgress
            );
        }
    }
}

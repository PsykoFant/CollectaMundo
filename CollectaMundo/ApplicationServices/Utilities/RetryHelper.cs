namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<bool> RetryLoopAsync(
            Func<int, Task<bool>> attemptFunc,
            int maxRetries = 3,
            IProgress<string>? stepNameProgress = null,  // StatusLabel3
            IProgress<string>? detailProgress = null,     // StatusLabel2
            string stepName = "")

        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                stepNameProgress?.Report(attempt == 1 ? stepName : $"{stepName} — Attempt {attempt}...");

                try
                {
                    if (await attemptFunc(attempt))
                        return true;
                }
                catch (Exception ex)
                {
                    detailProgress?.Report($"❌ {stepName} failed: {ex.Message}");
                }

                await Task.Delay(2000);
            }

            detailProgress?.Report($"❌ {stepName} failed after {maxRetries} retries.");
            return false;
        }
    }
}

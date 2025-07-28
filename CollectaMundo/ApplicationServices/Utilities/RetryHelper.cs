namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<bool> RetryLoopAsync(Func<int, Task<bool>> attemptFunc, int maxRetries = 3, IProgress<string>? progress = null, string stepName = "")
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                progress?.Report(attempt == 1 ? $"{stepName}..." : $"{stepName} — Attempt {attempt}...");

                try
                {
                    if (await attemptFunc(attempt))
                        return true;
                }
                catch (Exception ex)
                {
                    progress?.Report($"{stepName} failed: {ex.Message}");
                }

                await Task.Delay(2000);
            }

            progress?.Report($"{stepName} failed after {maxRetries} retries.");
            return false;
        }


    }
}

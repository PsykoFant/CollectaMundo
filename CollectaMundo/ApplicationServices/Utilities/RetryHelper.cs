using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<bool> RetryLoopAsync(Func<int, Task<bool>> attemptFunc, string stepName, int maxRetries = 3, IProgress<string>? progress = null)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                progress?.Report($"Step: {stepName} — attempt {attempt}...");

                try
                {
                    Debug.WriteLine($"[RetryLoopAsync] Step '{stepName}' attempt {attempt}...");

                    if (await attemptFunc(attempt))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    string message = $"Step '{stepName}' failed on attempt {attempt}: {ex.Message}";
                    progress?.Report(message);

                    Debug.WriteLine(message);
                }

                await Task.Delay(3000);
            }

            progress?.Report($"Step '{stepName}' failed after {maxRetries} tries.");
            return false;
        }
    }
}

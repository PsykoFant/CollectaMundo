using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<bool> RetryLoopAsync(Func<Task> stepWork, int maxRetries = 3, IProgress<string>? stepNameProgress = null, IProgress<string>? detailProgress = null, string stepName = "")
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                stepNameProgress?.Report(attempt == 1 ? stepName : $"{stepName} — Attempt {attempt}...");

                try
                {
                    await stepWork(); // 
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RetryLoopAsync] Step '{stepName}' attempt {attempt} threw: {ex.Message}");
                    detailProgress?.Report($"❌ Error: {ex.Message}");
                }

                await Task.Delay(3000);
            }
            return false;
        }
    }
}

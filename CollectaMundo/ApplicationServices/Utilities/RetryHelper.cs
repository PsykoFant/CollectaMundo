using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<OperationResult> RetryLoopAsync(Func<Task<OperationResult>> stepWork, int retryDelayInMs, string stepName, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                stepNameAndNumberProgress?.Report(attempt == 1 ? stepName : $"{stepName} — Attempt {attempt}...");
                try
                {
                    var result = await stepWork();

                    if (result.Code == OperationResultCode.Success)
                        return result;

                    stepDetailAndErrorProgress?.Report($"❌ {result.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RetryLoopAsync<OperationResult>] Step '{stepName}' attempt {attempt} threw: {ex.Message}");
                    stepDetailAndErrorProgress?.Report($"❌ Exception: {ex.Message}");
                }

                await Task.Delay(retryDelayInMs);
            }

            return new OperationResult(OperationResultCode.Error, $"{stepName} failed after {maxRetries} attempts.");
        }
    }

}

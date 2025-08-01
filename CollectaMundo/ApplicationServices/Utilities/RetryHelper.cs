using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class RetryHelper
    {
        public static async Task<OperationResult> RetryLoopAsync(Func<Task<OperationResult>> stepWork, int maxRetries = 3, IProgress<string>? stepNameProgress = null, IProgress<string>? detailProgress = null, string stepName = "")
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                stepNameProgress?.Report(attempt == 1 ? stepName : $"{stepName} — Attempt {attempt}...");
                try
                {
                    var result = await stepWork();

                    if (result.Code == OperationResultCode.Success)
                        return result;

                    detailProgress?.Report($"❌ {result.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RetryLoopAsync<OperationResult>] Step '{stepName}' attempt {attempt} threw: {ex.Message}");
                    detailProgress?.Report($"❌ Exception: {ex.Message}");
                }

                await Task.Delay(3000);
            }

            return new OperationResult(OperationResultCode.Error, $"{stepName} failed after {maxRetries} attempts.");
        }
    }

}

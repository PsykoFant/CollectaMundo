using CollectaMundo.ApplicationServices.Shared.Operation;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.Shared
{
    public static class RetryHelper
    {
        public static async Task<OperationResult> RetryLoopAsync(Func<Task<OperationResult>> stepWork, int retryDelayInMs, string stepName, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, int maxRetries = 3, CancellationToken cancelToken = default)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                stepNameAndNumberProgress?.Report(attempt == 1
                    ? stepName
                    : $"{stepName} — Attempt {attempt}...");

                try
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        stepDetailAndErrorProgress?.Report("❌ Cancel requested before step execution.");
                        return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled before download started.");
                    }

                    var result = await stepWork();

                    if (result.Code == OperationResultCode.Success)
                    {
                        return result;
                    }

                    if (result.Code == OperationResultCode.CancelledByUser)
                    {
                        return result; // skip retries on user cancel
                    }

                    stepDetailAndErrorProgress?.Report($"❌ {result.Message}");
                }
                catch (OperationCanceledException)
                {
                    stepDetailAndErrorProgress?.Report("❌ Cancelled during retry step.");
                    return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled during download retry.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RetryLoopAsync] Step '{stepName}' attempt {attempt} threw: {ex}");
                    stepDetailAndErrorProgress?.Report($"❌ Exception: {ex.Message}");
                }

                // Safe delay with cancellation — wrap in try/catch
                try
                {
                    await Task.Delay(retryDelayInMs, cancelToken);
                }
                catch (OperationCanceledException)
                {
                    stepDetailAndErrorProgress?.Report("❌ Cancelled during retry delay.");
                    return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled during retry delay.");
                }
            }

            return new OperationResult(OperationResultCode.Error, $"{stepName} failed after {maxRetries} attempts.");
        }

    }

}

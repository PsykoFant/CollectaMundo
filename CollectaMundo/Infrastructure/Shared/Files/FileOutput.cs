using System.IO;

namespace CollectaMundo.Infrastructure.Shared.Files
{
    public static class FileOutput
    {
        public static async Task WriteAtomicallyAsync(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
            var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, useAsync: true);

            try
            {
                await using (output)
                {
                    await writeContent(output);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            catch
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not remove temporary export '{temporaryPath}': {cleanupError.Message}");
                }

                throw;
            }
        }
    }
}

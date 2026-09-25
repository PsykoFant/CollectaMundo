namespace CollectaMundo.ApplicationServices.Shared.Files
{
    public interface ICsvFileWriter
    {
        Task WriteAsync(string filePath, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default);
        Task WriteAsync(string filePath, IReadOnlyList<string> headers, IAsyncEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default);
    }
}

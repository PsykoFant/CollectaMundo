namespace CollectaMundo.Infrastructure.CardImages
{
    public interface ICardImageDownloader
    {
        Task<byte[]?> DownloadAsync(string? url, string uuid, string side);
    }

}

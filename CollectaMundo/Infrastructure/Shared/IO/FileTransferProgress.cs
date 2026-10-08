namespace CollectaMundo.Infrastructure.Shared.IO
{
    public readonly record struct FileTransferProgress(long BytesTransferred, long? TotalBytes)
    {
        public int? Percent => TotalBytes is > 0 ? Math.Clamp((int)((double)BytesTransferred / TotalBytes.Value * 100), 0, 100) : null;
    }
}

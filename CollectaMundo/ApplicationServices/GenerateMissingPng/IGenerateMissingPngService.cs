namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public interface IGenerateMissingPngService
    {
        Task GenerateMissingManaSymbolImagesAsync(IProgress<int> percentProgress);
        Task GenerateMissingManaCostImagesAsync(IProgress<int> percentProgress);
        Task GenerateMissingKeyRuneImagesAsync(IProgress<int> percentProgress);
    }
}

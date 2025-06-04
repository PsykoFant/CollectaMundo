namespace CollectaMundo.DomainLogic.GenerateMissingPng
{
    public interface IGenerateMissingPngLogic
    {
        HashSet<string> ExtractSymbolsFromManaCosts(List<string> manaCosts);
        Task<byte[]> DownloadAndConvertSvgToPngAsync(string svgUrl);
    }
}

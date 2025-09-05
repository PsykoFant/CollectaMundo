namespace CollectaMundo.DomainLogic.GenerateMissingPng
{
    public interface IGenerateMissingPngLogic
    {
        HashSet<string> ExtractSymbolsFromManaCosts(List<string> manaCosts);
        Task<byte[]> ProcessManaCostInputAsync(string manaCostInput, Dictionary<string, byte[]> symbolImageMap);
        Task<byte[]> ConvertSvgToPngAsync(string svgContent);

    }
}

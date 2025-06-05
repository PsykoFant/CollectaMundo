using Newtonsoft.Json.Linq;

namespace CollectaMundo.DomainLogic.GenerateMissingPng
{
    public interface IGenerateMissingPngLogic
    {
        HashSet<string> ExtractSymbolsFromManaCosts(List<string> manaCosts);
        Task<byte[]> DownloadAndConvertSvgToPngAsync(string svgUrl);
        Task<byte[]> ProcessManaCostInputAsync(string manaCostInput, Dictionary<string, byte[]> symbolImageMap);
        Task<(string SetCode, byte[] PngData)> ProcessSetSvgAsync(string setCode, JArray? allSets);
    }
}

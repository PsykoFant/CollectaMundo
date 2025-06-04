using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.DomainLogic.GenerateMissingPng
{
    public class GenerateMissingPngLogic : IGenerateMissingPngLogic
    {
        private static readonly object SvgRenderLock = new();
        public async Task<byte[]> DownloadAndConvertSvgToPngAsync(string svgUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                string svgContent = await httpClient.GetStringAsync(svgUrl);
                using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = false,
                    TextAsGeometry = false,
                    OptimizePath = true
                };

                lock (SvgRenderLock)
                {
                    var reader = new FileSvgReader(settings);
                    var drawing = reader.Read(svgStream);

                    if (drawing == null)
                    {
                        Debug.WriteLine($"[PngLogic] Failed to parse SVG from {svgUrl}");
                        return [];
                    }

                    var drawingImage = new DrawingImage(drawing);
                    var drawingVisual = new DrawingVisual();

                    double aspectRatio = drawingImage.Width / drawingImage.Height;
                    int newHeight = 20;
                    int newWidth = (int)(newHeight * aspectRatio);

                    using (var context = drawingVisual.RenderOpen())
                    {
                        context.DrawImage(drawingImage, new Rect(0, 0, newWidth, newHeight));
                    }

                    var rtb = new RenderTargetBitmap(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(drawingVisual);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngLogic] Error downloading/converting SVG from {svgUrl}: {ex.Message}");
                return [];
            }
        }
        public HashSet<string> ExtractSymbolsFromManaCosts(List<string> manaCosts)
        {
            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var regex = new Regex(@"\{(.*?)\}");

            foreach (var cost in manaCosts)
            {
                if (string.IsNullOrWhiteSpace(cost)) continue;

                foreach (Match match in regex.Matches(cost))
                {
                    string symbol = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(symbol))
                        symbols.Add(symbol);
                }
            }

            return symbols;
        }


    }
}

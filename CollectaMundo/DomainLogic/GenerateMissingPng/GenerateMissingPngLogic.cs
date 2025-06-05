using Newtonsoft.Json.Linq;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
        // Mana symbols
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

        // Mana cost images
        public async Task<byte[]> ProcessManaCostInputAsync(string manaCostInput, Dictionary<string, byte[]> symbolImageMap)
        {
            List<Bitmap> manaSymbolImages = [];

            try
            {
                string[] symbols = manaCostInput.Trim('{', '}').Split(["}{"], StringSplitOptions.RemoveEmptyEntries);

                foreach (var symbol in symbols)
                {
                    if (symbolImageMap.TryGetValue(symbol, out byte[] imgBytes))
                    {
                        using MemoryStream ms = new(imgBytes);
                        Bitmap bmp = new(ms);
                        manaSymbolImages.Add(new Bitmap(bmp)); // Defensive clone
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngLogic] Error processing mana cost input '{manaCostInput}': {ex.Message}");
            }

            return await CombineImagesAsync(manaSymbolImages);
        }
        private static async Task<byte[]> CombineImagesAsync(List<Bitmap> images)
        {
            return await Task.Run(() => CombineImages(images));
        }
        private static byte[] CombineImages(List<Bitmap> images)
        {
            try
            {
                if (images == null || images.Count == 0)
                    return [];

                int width = images.Sum(img => img.Width);
                int height = images.Max(img => img.Height);

                using var combined = new Bitmap(width, height, images[0].PixelFormat);
                combined.SetResolution(images[0].HorizontalResolution, images[0].VerticalResolution);

                using (Graphics g = Graphics.FromImage(combined))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    int xOffset = 0;
                    foreach (var img in images)
                    {
                        g.DrawImage(img, new System.Drawing.Point(xOffset, 0));
                        xOffset += img.Width;
                    }
                }

                using var ms = new MemoryStream();
                combined.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngLogic] Error in CombineImages: {ex.Message}");
                return [];
            }
        }

        // Keyrune images



        public async Task<(string SetCode, byte[] PngData)> ProcessSetSvgAsync(string setCode, JArray? data)
        {
            try
            {
                var match = data?.FirstOrDefault(x =>
                    x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);

                string svgUrl = match?["icon_svg_uri"]?.ToString()
                              ?? "https://svgs.scryfall.io/sets/default.svg";

                byte[] pngData = await DownloadAndConvertSvgToPngAsync(svgUrl);
                return (SetCode: setCode, PngData: pngData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngLogic] Failed to process keyrune SVG for set {setCode}: {ex.Message}");
                return (setCode, []);
            }
        }

        // Shared
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
    }
}

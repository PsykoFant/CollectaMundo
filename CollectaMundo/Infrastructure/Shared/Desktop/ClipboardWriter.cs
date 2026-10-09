using CollectaMundo.ApplicationServices.Shared;
using System.Windows;

namespace CollectaMundo.Infrastructure.Shared.Desktop
{
    public sealed class ClipboardWriter : IClipboardWriter
    {
        public void SetText(string text)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            Clipboard.SetText(text);
        }
    }
}

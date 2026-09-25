using CollectaMundo.ApplicationServices.Shared;
using System.Windows;

namespace CollectaMundo.Infrastructure.Shared
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

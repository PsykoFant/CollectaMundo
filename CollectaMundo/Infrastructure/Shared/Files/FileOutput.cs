using System.IO;

namespace CollectaMundo.Infrastructure.Shared.Files
{
    public static class FileOutput
    {
        public static Stream OpenWrite(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        }
    }
}

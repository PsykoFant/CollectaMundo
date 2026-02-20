using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class TestFileSystemPicker(string pathToReturn) : IFileSystemPicker
    {
        private readonly string _pathToReturn = pathToReturn;

        public string? PickFile(
            string title,
            string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*")
        {
            return _pathToReturn;
        }

        public string? PickFolder(string title, string? initialPath = null)
        {
            throw new NotSupportedException("PickFolder is not used in import tests.");
        }

        public string? PickSaveFile(string title, string defaultFileName, string filter)
        {
            throw new NotSupportedException("PickSaveFile is not used in import tests.");
        }
    }
}

namespace CollectaMundo.Tests.TestUtils
{
    internal static class TestSqliteConnectionStrings
    {
        public static string SharedInMemory(string dbName)
        {
            return $"FullUri=file:{dbName}?mode=memory&cache=shared;Version=3;Pooling=False;";
        }
    }
}

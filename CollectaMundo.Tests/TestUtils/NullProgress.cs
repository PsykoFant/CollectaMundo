namespace CollectaMundo.Tests.TestUtils
{
    public class NullProgress<T> : IProgress<T>
    {
        public void Report(T value) { }
    }
}

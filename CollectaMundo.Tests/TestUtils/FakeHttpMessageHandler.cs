using System.IO;
using System.Net;
using System.Net.Http;

namespace CollectaMundo.Tests.TestUtils
{
    public class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc = handlerFunc;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handlerFunc(request, cancellationToken);
        public static HttpClient WithStaticResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handler = new FakeHttpMessageHandler(async (req, token) =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                };
                return await Task.FromResult(response);
            });

            return new HttpClient(handler);
        }
        public static HttpClient WithStatusCode(HttpStatusCode statusCode)
        {
            return WithStaticResponse("Error", statusCode);
        }

        public static HttpClient WithDelayedResponse(int delayMs = 1000)
        {
            var handler = new FakeHttpMessageHandler(async (req, token) =>
            {
                await Task.Delay(delayMs, token); // Simulate server/network delay
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ \"status\": \"delayed ok\" }")
                };
                return response;
            });

            return new HttpClient(handler);
        }

        public static HttpClient WithException(Exception ex)
        {
            var handler = new FakeHttpMessageHandler((req, token) =>
            {
                throw ex;
            });

            return new HttpClient(handler);
        }
        public static HttpClient WithStreamFailure(Exception exceptionToThrow)
        {
            var handler = new FakeHttpMessageHandler((req, token) =>
            {
                var failingStream = new FailingStream(exceptionToThrow);

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(failingStream)
                };

                return Task.FromResult(response);
            });

            return new HttpClient(handler);
        }

        // Add this helper class at the bottom of the same file:
        private class FailingStream : Stream
        {
            private readonly Exception _exception;
            public FailingStream(Exception exception)
            {
                _exception = exception;
            }

            public override int Read(byte[] buffer, int offset, int count) => throw _exception;
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw _exception;
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw _exception;

            // The rest are not used but must be implemented
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

    }
}

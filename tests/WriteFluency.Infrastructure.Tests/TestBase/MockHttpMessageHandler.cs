using System;

namespace WriteFluency.Infrastructure;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? HandlerFunc { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HandlerFunc is null)
            throw new InvalidOperationException("HandlerFunc was not set.");
        return HandlerFunc(request, cancellationToken);
    }
}

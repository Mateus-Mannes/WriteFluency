using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace WriteFluency.Infrastructure;

public abstract class InfrastructureTestBase
{
    protected readonly IServiceCollection Services;

    protected InfrastructureTestBase()
    {
        Services = new ServiceCollection();
        ConfigureServices(Services);
    }

    protected abstract void ConfigureServices(IServiceCollection services);

    protected TService GetService<TService>() where TService : class
    {
        var provider = Services.BuildServiceProvider();
        return provider.GetRequiredService<TService>();
    }

    protected static HttpClient CreateMockHttpClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
    {
        var handler = new MockHttpMessageHandler
        {
            HandlerFunc = handlerFunc
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
    }

}

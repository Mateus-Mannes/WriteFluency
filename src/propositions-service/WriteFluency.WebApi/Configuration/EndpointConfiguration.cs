using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Endpoints;

namespace WriteFluency.WebApi;

public static class EndpointConfiguration
{
    public static void UseEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointMappers = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IEndpointMapper).IsAssignableFrom(t) 
                        && !t.IsInterface 
                        && !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .Cast<IEndpointMapper>();

        var apiGroup = app.MapGroup("api")
            .WithMetadata(new ProducesAttribute("application/json"));

        foreach (var mapper in endpointMappers) mapper.MapEndpoints(apiGroup);
    }
}

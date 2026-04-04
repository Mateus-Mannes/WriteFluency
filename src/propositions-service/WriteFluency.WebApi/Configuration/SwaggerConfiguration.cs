namespace WriteFluency.WebApi;

public static class SwaggerConfiguration
{
    public static void AddAppSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen();
    }
}

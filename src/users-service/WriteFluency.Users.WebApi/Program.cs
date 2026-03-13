using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient(
    "wf-infra-redis",
    configureOptions: options =>
    {
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;
    });
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddUsersPersistence(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "WriteFluency Users API");
        options.RoutePrefix = "swagger";
    });
}

app.UsePathBase("/users");
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;

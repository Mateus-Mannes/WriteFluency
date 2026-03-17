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

var usersApi = app.MapGroup("/users");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/users/openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/users/openapi/v1.json", "WriteFluency Users API");
        options.RoutePrefix = "users/swagger";
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

usersApi.MapAuthEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;

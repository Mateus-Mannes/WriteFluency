var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UsePathBase("/users");

app.UseHttpsRedirection();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "wf-users-api"
    });
})
.WithName("GetUsersHealth");

app.Run();

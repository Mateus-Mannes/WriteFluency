using WriteFluency.Users.WebApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddUsersPersistence(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UsePathBase("/users");
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.Run();

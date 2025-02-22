using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WriteFluencyApi.Configuration;
using WriteFluencyApi.Data;
using WriteFluencyApi.Domain.Login;
using WriteFluencyApi.ExternalApis.OpenAI;
using WriteFluencyApi.ExternalApis.TextToSpeech;
using WriteFluencyApi.ListenAndWrite.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAppSwagger();

builder.AddAppAuthentication();

// Options configuration
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.Configure<TextToSpeechOptions>(builder.Configuration.GetSection(TextToSpeechOptions.Section));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

// Adds the database context and identity configuration
builder.Services.AddDbContext<ApiDbContext>(opts => opts.UseSqlite("Data Source=data.db"));
builder.Services.AddIdentityCore<IdentityUser>()
    .AddApiEndpoints()
    .AddEntityFrameworkStores<ApiDbContext>();

// Adding domain services
builder.Services.AddHttpClient();
builder.Services.AddTransient<ITextGenerator, OpenAIApi>();
builder.Services.AddTransient<ISpeechGenerator, TextToSpeechApi>();
builder.Services.AddTransient<ILevenshteinDistanceService, LevenshteinDistanceService>();
builder.Services.AddTransient<ITokenAlignmentService, TokenAlignmentService>();
builder.Services.AddTransient<ITokenizeTextService, TokenizeTextService>();
builder.Services.AddTransient<INeedlemanWunschAlignmentService, NeedlemanWunschAlignmentService>();
builder.Services.AddTransient<ITextComparisonService, TextComparisonService>();
builder.Services.AddTransient<ITextAlignmentService, TextAlignmentService>();
builder.Services.AddTransient<ITokenComparisonService, TokenComparisonService>();
builder.Services.AddTransient<LoginService>();

builder.Services.AddCors();

var app = builder.Build();

var clients = app.Configuration.GetValue<string>("AllowedClients")?.Split(',');
app.UseCors(opts =>
{
    if (clients![0] != "*") opts.WithOrigins(clients);
    else opts.AllowAnyOrigin();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        // Configures the swagger ui to use the google login
        c.OAuthClientId(builder.Configuration["Authentication:Google:ClientId"]);
        c.OAuthClientSecret(builder.Configuration["Authentication:Google:ClientSecret"]);
        c.OAuthUsePkce(); 
        c.OAuthScopes("openid", "profile", "email");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

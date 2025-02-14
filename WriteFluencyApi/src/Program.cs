using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WriteFluencyApi.Data;
using WriteFluencyApi.Domain.Login;
using WriteFluencyApi.ExternalApis.OpenAI;
using WriteFluencyApi.ExternalApis.TextToSpeech;
using WriteFluencyApi.ListenAndWrite.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add configs
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.Configure<TextToSpeechOptions>(builder.Configuration.GetSection(TextToSpeechOptions.Section));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

// add services

builder.Services.AddDbContext<ApiDbContext>(opts => opts.UseSqlite("Data Source=data.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddIdentityCore<IdentityUser>()
    .AddApiEndpoints()
    .AddEntityFrameworkStores<ApiDbContext>();

builder.Services.AddAuthorization();

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

var clients = app.Configuration.GetValue<string>("AlloewdClients")?.Split(',');
app.UseCors(opts =>
{
    if (clients![0] != "*") opts.WithOrigins(clients);
    else opts.AllowAnyOrigin();
    opts.AllowAnyMethod();
    opts.AllowAnyHeader();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

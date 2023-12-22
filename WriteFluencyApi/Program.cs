using WriteFluencyApi.ExternalApis.OpenAI;
using WriteFluencyApi.ExternalApis.TextToSpeech;
using WriteFluencyApi.ListenAndWrite.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<ITextGenerator, OpenAIApi>();
builder.Services.AddScoped<ISpeechGenerator, TextToSpeechApi>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add configs
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<OpenAIConfig>(builder.Configuration.GetSection(OpenAIConfig.Config));
builder.Services.Configure<TextToSpeechConfig>(builder.Configuration.GetSection(TextToSpeechConfig.Config));

// add services
builder.Services.AddHttpClient();
builder.Services.AddTransient<LevenshteinDistanceService>();
builder.Services.AddTransient<TokenAlignmentService>();
builder.Services.AddTransient<TokenizeTextService>();
builder.Services.AddTransient<NeedlemanWunschAlignmentService>();
builder.Services.AddTransient<TextComparisionService>();

builder.Services.AddCors();

var app = builder.Build();

var clients = app.Configuration.GetValue<string>("AlloewdClients")?.Split(',');
app.UseCors(opts => {
    if(clients![0] != "*") opts.WithOrigins(clients);
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

app.UseAuthorization();

app.MapControllers();

app.Run();

using WriteFluencyApi.ExternalApis.OpenAI;
using WriteFluencyApi.ExternalApis.TextToSpeech;
using WriteFluencyApi.Services.ListenAndWrite;

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

var app = builder.Build();

var clients = app.Configuration.GetValue<string>("AlloewdClients")?.Split(',');
app.UseCors(x => x
    .WithOrigins(clients ?? new string[0])
    .AllowAnyMethod()
    .AllowAnyHeader());

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

using WriteFluencyApi.ExternalApis.OpenAI;
using WriteFluencyApi.Services.ListenAndWrite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<ITextGenerator, OpenAIApi>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add configs
builder.Services.Configure<OpenAIConfig>(builder.Configuration.GetSection(OpenAIConfig.Config));

var app = builder.Build();

var clients = app.Configuration.GetValue<string>("AlloewdClients").Split(',');
app.UseCors(x => x
    .WithOrigins(clients)
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

using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using WriteFluency.UsersProgressService.Configuration;
using WriteFluency.UsersProgressService.Progress;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, configurationBuilder) =>
    {
        configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        configurationBuilder.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
        configurationBuilder.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<WorkerOptions>(workerOptions =>
        {
            workerOptions.Serializer = new JsonObjectSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        });

        services.AddUsersProgressService(context.Configuration);
    })
    .Build();

await host.RunAsync();

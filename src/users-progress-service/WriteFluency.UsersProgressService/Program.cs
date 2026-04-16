using WriteFluency.UsersProgressService.Configuration;
using WriteFluency.UsersProgressService.Progress;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddUsersProgressService(context.Configuration);
    })
    .Build();

await host.RunAsync();
using Microsoft.Extensions.Hosting; 
using Microsoft.Extensions.Hosting.WindowsServices; 
using Microsoft.Extensions.DependencyInjection; 

var builder = Host.CreateDefaultBuilder(args) 
    .UseWindowsService() 
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<Worker>(); 
    });

await builder.Build().RunAsync();
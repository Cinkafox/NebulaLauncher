using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared;

namespace Nebula.Runner;

public static class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddServices();
        
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<App>()!.Run(args);
    }
}
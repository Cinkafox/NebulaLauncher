using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared;
using Nebula.SharedModels;

namespace Nebula.UnitTest;

public static class TestServiceHelper
{
    public static SharedUnit GetSharedUnit(Action<IServiceCollection> beforeServiceBuild)
    {
        var services = new ServiceCollection();
        beforeServiceBuild.Invoke(services);
        services.AddServices();
        
        var serviceProvider = services.BuildServiceProvider();
        return new SharedUnit(serviceProvider);
    }

    public static void InitFileServiceTest()
    {
        var path = Path.Combine(Path.GetTempPath(), "tempThink"+Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        AppDataPath.SetTestRootPath(path);
    }
}

public class SharedUnit
{
    public SharedUnit(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
    
    public T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
}
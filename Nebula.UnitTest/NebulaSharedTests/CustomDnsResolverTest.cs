using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.UnitTest.NebulaSharedTests;

public class CustomDnsResolverTest : BaseSharedTest
{
    private ConfigurationService _conVarService;
    
    public override void BeforeServiceBuild(IServiceCollection services)
    {
        
    }
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _conVarService = _sharedUnit.GetService<ConfigurationService>();
    }

    [Test]
    public async Task GetTest()
    {
        var hub = await _sharedUnit.GetService<RestService>().GetAsync<ModulesInfo>(new Uri("https://robust-builds.cdn.spacestation14.com/modules.json"), CancellationToken.None);
        Console.WriteLine(hub.Modules.Count);
        Assert.IsNotNull(hub);
    }
}
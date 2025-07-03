using Microsoft.Extensions.DependencyInjection;

namespace Nebula.UnitTest.NebulaSharedTests;

public abstract class BaseSharedTest
{
    protected SharedUnit _sharedUnit = default!;
    
    public abstract void BeforeServiceBuild(IServiceCollection services);
    
    public virtual void Setup()
    {
        _sharedUnit = TestServiceHelper.GetSharedUnit(BeforeServiceBuild);
    }
}


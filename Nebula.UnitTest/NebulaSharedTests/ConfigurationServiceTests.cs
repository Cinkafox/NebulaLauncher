using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared.Services;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(typeof(ConfigurationService))]
public sealed class ConfigurationServiceTests: BaseSharedTest
{
    private ConfigurationService _conVarService;

    public override void BeforeServiceBuild(IServiceCollection services)
    {
        TestServiceHelper.InitFileServiceTest();
    }

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _conVarService = _sharedUnit.GetService<ConfigurationService>();
    }

    [Test]
    public void GetDefaultConVarTest()
    {
        var value = _conVarService.GetConfigValue(TestConVar.SimpleConvar);
        Assert.NotNull(value);
        Assert.That(value, Is.EqualTo(TestConVar.SimpleConvar.DefaultValue));
    }

    [Test]
    public void GetNullConVarTest()
    {
        var value = _conVarService.GetConfigValue(TestConVar.NullConvar);
        Assert.Null(value);
    }

    [Test]
    public void WriteConVarTest()
    {
        var value = _conVarService.GetConfigValue(TestConVar.SimpleConvar);
        Assert.That(value, Is.EqualTo(TestConVar.SimpleConvar.DefaultValue));
        
        _conVarService.SetConfigValue(TestConVar.SimpleConvar, "notdefault");
        value = _conVarService.GetConfigValue(TestConVar.SimpleConvar);
        Assert.That(value, Is.Not.EqualTo(TestConVar.SimpleConvar.DefaultValue));
        Assert.That(value, Is.EqualTo("notdefault"));
        
        _conVarService.SetConfigValue(TestConVar.SimpleConvar, null);
        
        value = _conVarService.GetConfigValue(TestConVar.SimpleConvar);
        Assert.That(value, Is.EqualTo(TestConVar.SimpleConvar.DefaultValue));
    }

    [Test]
    public void WriteComplexConvarTest()
    {
        var testVar = new TestVarObject("Alex", 2);
        _conVarService.SetConfigValue(TestConVar.TestVarObject, testVar);
        var value = _conVarService.GetConfigValue(TestConVar.TestVarObject);
        Assert.That(value, Is.EqualTo(testVar));
        
        _conVarService.SetConfigValue(TestConVar.TestVarObject, default);
    }

    [Test]
    public void WriteArrayConvarTest()
    {
        var testVarArr = new[] { new TestVarObject("Alex", 2), new TestVarObject("Vitya", 3) };
        _conVarService.SetConfigValue(TestConVar.TestVarArray, testVarArr);
        var value = _conVarService.GetConfigValue(TestConVar.TestVarArray);
        Assert.NotNull(value);
        Assert.That(testVarArr.SequenceEqual(value));
        _conVarService.SetConfigValue(TestConVar.TestVarArray, null);
    }
    
    
}

public static class TestConVar
{
    public static ConVar<string> SimpleConvar = ConVarBuilder.Build("test.convarsimple", "test");
    public static ConVar<string?> NullConvar = ConVarBuilder.Build<string?>("test.convarsimplenull");
    
    public static ConVar<TestVarObject> TestVarObject = ConVarBuilder.Build<TestVarObject>("test.convarobject", default);
    public static ConVar<TestVarObject[]> TestVarArray = ConVarBuilder.Build<TestVarObject[]>("test.convarobject.array");
}

public record struct TestVarObject(string Name, int Count);
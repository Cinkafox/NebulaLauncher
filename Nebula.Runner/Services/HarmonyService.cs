using System.Data;
using HarmonyLib;
using Nebula.Shared;

namespace Nebula.Runner.Services;

[ServiceRegister]
public class HarmonyService(ReflectionService reflectionService)
{
    private HarmonyInstance? _instance;

    public HarmonyInstance Instance
    {
        get
        {
            if (_instance is null) 
                CreateInstance();
            return _instance!;
        }
    }

    public void CreateInstance()
    {
        if (_instance is not null) 
            throw new Exception();
        
        _instance = new HarmonyInstance();
        UnShittyWizard();
    }

    /// <summary>
    /// Я не понимаю суть античитов в сосаке.
    /// Эту хуйню может обойти любой школьник!
    /// Нет.. я не хочу вводить читы, просто мне нужно поменять некоторые штучки :)
    /// </summary>
    private void UnShittyWizard()
    {
        var method = reflectionService.GetType("Robust.Client.GameController").TypeInitializer;
        _instance!.Harmony.Patch(method, new HarmonyMethod(IgnorePrefix));
        
        var method2 = typeof(Type).Method(nameof(Type.GetType), new[] { typeof(string) });
        _instance!.Harmony.Patch(method2, new HarmonyMethod(HidifyPrefix));
    }
    
    static bool IgnorePrefix()
    {
        return false;
    }
    
    static bool HidifyPrefix(ref Type? __result, string typeName)
    {
        if (typeName.Contains("Harmony"))
        {
            __result = null;
            return false;
        }
        
        return true;
    }
}


public class HarmonyInstance
{
    public readonly Harmony Harmony;

    internal HarmonyInstance()
    {
        Harmony = new Harmony("ru.cinka.patch");
    }
}
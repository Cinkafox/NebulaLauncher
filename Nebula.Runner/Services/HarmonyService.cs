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
    /// Я помню пенис большой,Я помню пенис большой, Я помню пенис большой, я помню....
    /// </summary>
    private void UnShittyWizard()
    {
        var method = reflectionService.GetType("Robust.Client.GameController").TypeInitializer;
        _instance!.Harmony.Patch(method, new HarmonyMethod(Prefix));
    }
    
    static bool Prefix()
    {
        return false;
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
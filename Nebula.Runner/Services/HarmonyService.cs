using HarmonyLib;
using Nebula.Shared;

namespace Nebula.Runner.Services;

[ServiceRegister]
public class HarmonyService
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
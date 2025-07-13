using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using Nebula.Shared;
using Nebula.Shared.FileApis;
using Nebula.Shared.Services;
using NLua;

namespace Nebula.Runner.Services;

[ServiceRegister]
public class ScriptService
{
    private readonly HarmonyService _harmonyService;
    private readonly ReflectionService _reflectionService;
    private readonly AssemblyService _assemblyService;

    private readonly FileApi _scriptFileApi;
    
    private static Dictionary<MethodBase, ScriptManifestDict> _scriptCache = [];
    private static Dictionary<string, Action> _assemblyLoadingQuery = [];

    public ScriptService(HarmonyService harmonyService, ReflectionService reflectionService, FileService fileService, AssemblyService assemblyService)
    {
        _harmonyService = harmonyService;
        _reflectionService = reflectionService;
        _assemblyService = assemblyService;

        _scriptFileApi = fileService.CreateFileApi("scripts");
        _assemblyService.OnAssemblyLoaded += OnAssemblyLoaded;
    }

    private void OnAssemblyLoaded(Assembly obj)
    {
        var objName = obj.GetName().Name ?? string.Empty;
        if (!_assemblyLoadingQuery.TryGetValue(objName, out var a)) return;
        Console.WriteLine("Inject assembly: " + objName);
        a();
        _assemblyLoadingQuery.Remove(objName);
    }

    public void LoadScripts()
    {
        Console.WriteLine("Loading scripts... " + _scriptFileApi.EnumerateDirectories("").Count());
        foreach (var dir in _scriptFileApi.EnumerateDirectories(""))
        {
            LoadScript(dir);
        }
    }
    
    public void LoadScript(string name)
    {
        Console.WriteLine($"Reading script {name}");
        var manifests = ReadManifest(name);
        
        foreach (var entry in manifests)
        {
            if (entry.TypeInitializer.HasValue) LoadTypeInitializer(entry.TypeInitializer.Value, name);
            if (entry.Method.HasValue) LoadMethod(entry.Method.Value, name);
        }
    }

    private void LoadTypeInitializer(ScriptMethodInjectItem item, string name)
    {
        Console.WriteLine($"Loading Initializer injection {name}...");
        var assemblyName = _reflectionService.ExtrackPrefix(item.Method.Class);
        
        if (!_assemblyService.Assemblies.Select(a => a.GetName().Name).Contains(assemblyName))
        {
            _assemblyLoadingQuery.Add(assemblyName, () => LoadTypeInitializer(item, name));
            return;
        }
        
        var targetType = _reflectionService.GetType(item.Method.Class);
        var method = targetType.TypeInitializer;
        InitialiseShared(method!, name, item);
    }

    private void LoadMethod(ScriptMethodInjectItem item, string name)
    {
        Console.WriteLine($"Loading method injection {name}...");
        var assemblyName = _reflectionService.ExtrackPrefix(item.Method.Class);
        
        if (!_assemblyService.Assemblies.Select(a => a.GetName().Name).Contains(assemblyName))
        {
            _assemblyLoadingQuery.Add(assemblyName, () => LoadMethod(item, name));
            return;
        }
        
        var targetType = _reflectionService.GetType(item.Method.Class);
        var method = targetType.GetMethod(item.Method.Method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        InitialiseShared(method!, name, item);
    }

    private void InitialiseShared(MethodBase method, string scriptName, ScriptMethodInjectItem item)
    {
        var scriptCode = File.ReadAllText(Path.Combine(_scriptFileApi.RootPath, scriptName, item.Script.LuaFile));
        
        var methodInfo = method as MethodInfo;
        HarmonyMethod dynamicPatch;
        
        if (methodInfo == null || methodInfo.ReturnType == typeof(void))
            dynamicPatch = new HarmonyMethod(typeof(ScriptService).GetMethod(nameof(LuaPrefix), BindingFlags.Static | BindingFlags.NonPublic));
        else
            dynamicPatch = new HarmonyMethod(typeof(ScriptService).GetMethod(nameof(LuaPrefixResult), BindingFlags.Static | BindingFlags.NonPublic));
        
        _scriptCache[method] = new ScriptManifestDict(scriptCode, item);

        _harmonyService.Instance.Harmony.Patch(method, prefix: dynamicPatch);
        Console.WriteLine($"Injected {scriptName}");
    }

    private ScriptEntry[] ReadManifest(string scriptName)
    {
        if(!_scriptFileApi.TryOpen(Path.Join(scriptName, "MANIFEST.json"), out var stream)) 
            throw new FileNotFoundException(Path.Join(scriptName, "MANIFEST.json") + " not found manifest!");
        
        return JsonSerializer.Deserialize<ScriptEntry[]>(stream) ?? [];
    }
    
    private static bool LuaPrefix(MethodBase __originalMethod, object __instance)
    {
        if (!_scriptCache.TryGetValue(__originalMethod, out var luaCode))
            return true;

        using var lua = new Lua();

        lua["this"] = __instance;

        var results = lua.DoString(luaCode.Code);
        
        if (results is { Length: > 0 } && results[0] is bool b)
            return b;
        
        return luaCode.ScriptMethodInjectItem.ContinueAfterInject;
    }
    
    private static bool LuaPrefixResult(MethodBase __originalMethod, object __instance, ref object __result)
    {
        if (!_scriptCache.TryGetValue(__originalMethod, out var luaCode))
            return true;

        using var lua = new Lua();

        lua["this"] = __instance;
        lua["result"] = __result;

        var results = lua.DoString(luaCode.Code);

        if (lua["result"] != null)
            __result = lua["result"];
        
        if (results is { Length: > 0 } && results[0] is bool b)
            return b;
        
        return luaCode.ScriptMethodInjectItem.ContinueAfterInject;
    }
}

public record struct ScriptManifestDict(string Code, ScriptMethodInjectItem ScriptMethodInjectItem);

public record struct ScriptEntry(
    [property: JsonPropertyName("method")] ScriptMethodInjectItem? Method,
    [property: JsonPropertyName("type_initializer")] ScriptMethodInjectItem? TypeInitializer
    );

public record struct ScriptMethodInjectItem(
    [property: JsonPropertyName("method")] ScriptMethodInfo Method,
    [property: JsonPropertyName("continue")] bool ContinueAfterInject, 
    [property: JsonPropertyName("script")] LuaMethodEntry Script
    );

public record struct ScriptMethodInfo(
    [property: JsonPropertyName("class")] string Class, 
    [property: JsonPropertyName("method")] string Method
    );

public record struct LuaMethodEntry(
    [property: JsonPropertyName("lua_file")] string LuaFile
    );
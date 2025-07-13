using System.Reflection;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Runner.Services;

[ServiceRegister]
public class ReflectionService
{
    private readonly Dictionary<string, Assembly> _typeCache = new();

    public ReflectionService(AssemblyService assemblyService)
    {
        assemblyService.OnAssemblyLoaded += OnAssemblyLoaded;
    }

    private void OnAssemblyLoaded(Assembly obj)
    {
        RegisterAssembly(obj);
    }

    public void RegisterAssembly(Assembly robustAssembly)
    {
        _typeCache.Add(robustAssembly.GetName().Name!, robustAssembly);
    }
    
    public Type? GetTypeImp(string name)
    {
        foreach (var (prefix,assembly) in _typeCache)
        {
            string appendedName = prefix + name;
            var theType = assembly.GetType(appendedName);
            if (theType != null)
            {
                return theType;
            }
        }

        return null;
    }

    public Type GetType(string name)
    {
        var prefix = ExtrackPrefix(name);
        return !_typeCache.TryGetValue(prefix, out var assembly)
            ? GetTypeImp(name)!
            : assembly.GetType(name)!;
    }

    public string ExtrackPrefix(string path)
    {
        var sp = path.Split(".");
        return sp[0] + "." + sp[1];
    }
}
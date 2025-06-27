using System.Reflection;
using Nebula.Shared;
using Nebula.Shared.FileApis;
using Nebula.Shared.Services;

namespace Nebula.Runner.Services;

[ServiceRegister]
public class ReflectionService(AssemblyService assemblyService)
{
    private Dictionary<string, Assembly> _typeCache = new();

    public void RegisterAssembly(Assembly robustAssembly)
    {
        _typeCache.Add(robustAssembly.GetName().Name!, robustAssembly);
    }

    public void RegisterRobustAssemblies(AssemblyApi engine)
    {
        RegisterAssembly(GetRobustAssembly("Robust.Shared", engine));
        RegisterAssembly(GetRobustAssembly("Robust.Client", engine));
    }

    private Assembly GetRobustAssembly(string assemblyName, AssemblyApi engine)
    {
        if(!assemblyService.TryOpenAssembly(assemblyName, engine, out var assembly))
            throw new Exception($"Unable to locate {assemblyName}.dll in engine build!");
        return assembly;
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

    private string ExtrackPrefix(string path)
    {
        var sp = path.Split(".");
        return sp[0] + "." + sp[1];
    }
}
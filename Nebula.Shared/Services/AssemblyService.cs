using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Nebula.Shared.FileApis;
using Nebula.Shared.Services.Logging;
using Robust.LoaderApi;
using SharpZstd.Interop;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class AssemblyService
{
    private readonly Dictionary<string, Assembly> _assemblyCache = new();
    private readonly ILogger _logger;

    private readonly HashSet<string> _resolvingAssemblies = new();

    private List<AssemblyApi> _mountedApis = [];
    
    public Action<Assembly>? OnAssemblyLoaded;
    public IReadOnlyList<Assembly> Assemblies => _assemblyCache.Values.ToList().AsReadOnly();

    public AssemblyService(DebugService debugService)
    {
        _logger = debugService.GetLogger(this);
        
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += LoadContextOnResolvingUnmanaged;
        AssemblyLoadContext.Default.Resolving += (context, name) => OnAssemblyResolving(context, name);
        
        ZstdImportResolver.ResolveLibrary += (name, assembly1, path) =>
        {
            if (name.Equals("SharpZstd.Native"))
            {
                _logger.Debug("RESOLVING SHARPZSTD THINK: " + name + " " + path);
                GetRuntimeInfo(out var platform, out var architecture, out var extension);
                var fileName = GetDllName(platform, architecture, extension);

                if (NativeLibrary.TryLoad(fileName, assembly1, path, out var nativeLibrary)) return nativeLibrary;
            }

            return IntPtr.Zero;
        };
    }

    private Assembly? OnAssemblyResolving(AssemblyLoadContext context, AssemblyName name)
    {
        if (_resolvingAssemblies.Contains(name.FullName))
        {
            _logger.Debug($"Already resolving {name.Name}, skipping.");
            return null; // Prevent recursive resolution
        }
        
        Assembly? assembly;

        if (_assemblyCache.TryGetValue(name.Name ?? "", out assembly))
        {
            return assembly;
        }
        
        foreach (var api in _mountedApis)
        {
            if((assembly = OnAssemblyResolving(context, name, api)) != null)
                return assembly;
        }
        
        return null;
    }

    public AssemblyApi Mount(IFileApi fileApi)
    {
        var asmApi = new AssemblyApi(fileApi);
        _mountedApis.Add(asmApi);
        return asmApi;
    }

    public bool TryGetLoader(Assembly clientAssembly, [NotNullWhen(true)] out ILoaderEntryPoint? loader)
    {
        loader = null;
    
        var attrib = clientAssembly.GetCustomAttribute<LoaderEntryPointAttribute>();
        if (attrib == null)
        {
            Console.WriteLine("No LoaderEntryPointAttribute found on Robust.Client assembly!");
            return false;
        }

        var type = attrib.LoaderEntryPointType;
        if (!type.IsAssignableTo(typeof(ILoaderEntryPoint)))
        {
            Console.WriteLine("Loader type '{0}' does not implement ILoaderEntryPoint!", type);
            return false;
        }

        loader = (ILoaderEntryPoint)Activator.CreateInstance(type)!;
        return true;
    }

    public bool TryOpenAssembly(string name, AssemblyApi assemblyApi, [NotNullWhen(true)] out Assembly? assembly)
    {
        if (!TryOpenAssemblyStream(name, assemblyApi, out var asm, out var pdb))
        {
            assembly = null;
            return false;
        }

        assembly = AssemblyLoadContext.Default.LoadFromStream(asm, pdb);
        _logger.Log("LOADED ASSEMBLY " + name);
        
        if (_assemblyCache.TryAdd(name, assembly))
        {
            OnAssemblyLoaded?.Invoke(assembly);
        }

        asm.Dispose();
        pdb?.Dispose();
        return true;
    }

    public bool TryOpenAssemblyStream(string name, AssemblyApi assemblyApi, [NotNullWhen(true)] out Stream? asm,
        out Stream? pdb)
    {
        asm = null;
        pdb = null;

        if (!assemblyApi.TryOpen($"{name}.dll", out asm))
            return false;

        assemblyApi.TryOpen($"{name}.pdb", out pdb);
        return true;
    }

    private Assembly? OnAssemblyResolving(AssemblyLoadContext context, AssemblyName name, AssemblyApi assemblyApi)
    {
        lock (_resolvingAssemblies)
        {
            try
            {
                _resolvingAssemblies.Add(name.FullName);
                _logger.Debug($"Resolving assembly from FileAPI: {name.Name}");
                return TryOpenAssembly(name.Name!, assemblyApi, out var assembly) ? assembly : null;
            }
            finally
            {
                _resolvingAssemblies.Remove(name.FullName);
            }
        }
    }

    private IntPtr LoadContextOnResolvingUnmanaged(Assembly assembly, string unmanaged)
    {
        var ourDir = Path.GetDirectoryName(typeof(AssemblyApi).Assembly.Location);
        var a = Path.Combine(ourDir!, unmanaged);

        _logger.Debug($"Loading dll lib: {a}");

        if (NativeLibrary.TryLoad(a, out var handle))
            return handle;

        _logger.Error("Loading dll error! Not found");

        return IntPtr.Zero;
    }

    public static string GetDllName(
        string platform,
        string architecture,
        string extension)
    {
        var name = $"SharpZstd.Native.{extension}";
        return name;
    }

    public static void GetRuntimeInfo(
        out string platform,
        out string architecture,
        out string extension)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform = "win";
            extension = "dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platform = "linux";
            extension = "so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = "osx";
            extension = "dylib";
        }
        else
        {
            platform = "linux";
            extension = "so";
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            architecture = "x64";
        else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            architecture = "x86";
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm)
            architecture = "arm";
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            architecture = "arm64";
        else
            throw new PlatformNotSupportedException("Unsupported process architecture.");
    }
}
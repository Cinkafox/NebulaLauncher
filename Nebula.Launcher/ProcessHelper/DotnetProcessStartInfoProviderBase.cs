using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ProcessHelper;

public abstract class DotnetProcessStartInfoProviderBase(DotnetResolverService resolverService) : IProcessStartInfoProvider
{
    protected abstract string GetDllPath();
    
    public virtual async Task<ProcessStartInfo> GetProcessStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = await resolverService.EnsureDotnet(),
            Arguments = GetDllPath(),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
    }
}
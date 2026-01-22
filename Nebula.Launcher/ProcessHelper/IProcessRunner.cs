using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Nebula.Launcher.ProcessHelper;

public interface IProcessStartInfoProvider
{
    public Task<ProcessStartInfo> GetProcessStartInfo(CancellationToken cancellationToken = default);
}
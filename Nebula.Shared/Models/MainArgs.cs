using Robust.LoaderApi;

namespace Nebula.Shared.Models;

public sealed class MainArgs : IMainArgs
{
    public MainArgs(string[] args, IFileApi fileApi, IRedialApi? redialApi, IEnumerable<ApiMount>? apiMounts)
    {
        Args = args;
        FileApi = fileApi;
        RedialApi = redialApi;
        ApiMounts = apiMounts;
    }

    public string[] Args { get; }
    public IFileApi FileApi { get; }
    public IRedialApi? RedialApi { get; }
    public IEnumerable<ApiMount>? ApiMounts { get; }
}
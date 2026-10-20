using System;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.Models.ContentBrowser;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

public sealed class LazyContentEntry : IContentEntry
{
    public IContentHolder Holder { get; set; }
    public IContentEntry? Parent { get; set; }
    public string? Name { get; }
    public string IconPath { get; }

    private readonly IContentEntry _lazyEntry;
    private readonly Action _lazyEntryInit;

    public LazyContentEntry (IContentHolder holder,string name, IContentEntry entry, Action lazyEntryInit)
    {
        Holder = holder;
        Name = name;
        IconPath = entry.IconPath;
        _lazyEntry = entry;
        _lazyEntryInit = lazyEntryInit;
    }
    public async Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken)
    {
        _lazyEntryInit?.Invoke();
        return _lazyEntry;
    }
}
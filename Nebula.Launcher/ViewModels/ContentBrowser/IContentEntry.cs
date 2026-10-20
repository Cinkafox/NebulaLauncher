using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.Models.ContentBrowser;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

public interface IContentEntry
{
    public IContentHolder Holder { get; }
    
    public IContentEntry? Parent { get; set; }
    public string? Name { get; }
    public string IconPath { get; }
    public ContentPath FullPath => Parent?.FullPath.With(Name) ?? new ContentPath(Name);
    
    public Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken = default);
    
    public async void GoCurrent()
    {
        var entry = await Go(ContentPath.Empty, CancellationToken.None);
        if(entry is not null) Holder.CurrentEntry = entry;
    }
    
    public IContentEntry GetRoot()
    {
        if (Parent is null) return this;
        return Parent.GetRoot();
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.Models.ContentBrowser;
using Nebula.Launcher.Services;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

public abstract class BaseFolderContentEntry : ViewModelBase, IContentEntry
{
    public bool IsLoading { get; set; } = false;
    public abstract ViewHelperService ViewHelperService { get; }
    
    public ObservableCollection<IContentEntry> Entries { get; } = [];

    private Dictionary<string, IContentEntry> _childs = [];

    public string IconPath => "/Assets/icon/folder.png";
    
    private IContentHolder? _holder = null;
    public IContentHolder Holder
    {
        get
        {
            if(_holder == null) 
                throw new InvalidOperationException(
                    GetType().Name + " was not initialised! Call Init(IContentHolder holder, string? name = null) before using it.");
            
            return _holder;
        }
    }

    public IContentEntry? Parent { get; set; }
    public string? Name { get; private set; }
    
    public async Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken = default)
    {
        if (path.IsEmpty()) return this;
        if (_childs.TryGetValue(path.GetNext(), out var child)) 
            return await child.Go(path, cancellationToken);
        
        return null;
    }

    public void Init(IContentHolder holder, string? name = null)
    {
        Name = name;
        _holder = holder;
    }

    public T AddChild<T>(T child) where T: IContentEntry
    {
        if(child.Name is null) throw new InvalidOperationException();
        
        child.Parent = this;
        
        _childs.Add(child.Name, child);
        Entries.Add(child);

        return child;
    }

    public bool TryGetChild(string name,[NotNullWhen(true)] out IContentEntry? child)
    {
        return _childs.TryGetValue(name, out child);
    }
    
    public bool TryOpenFile<T>(string file,[NotNullWhen(true)] out T? contentEntry) where T : BaseFileContentEntry
    {
        contentEntry = null;
        var entry = _childs.GetValueOrDefault(file);
        if (entry is not T t) return false;
        contentEntry = t;
        return true;
    }
}
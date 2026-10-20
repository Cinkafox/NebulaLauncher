using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Models.ContentBrowser;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.ContentBrowser;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ContentBrowserView))]
[ConstructGenerator]
public sealed partial class ContentBrowserViewModel : ViewModelBase, IContentHolder
{
    [ObservableProperty] private string _serverText = "";
    [ObservableProperty] private string _searchText = "";
    
    [GenerateProperty] private PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private CancellationService CancellationService { get; set; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private DecompilerService DecompilerService { get; } = default!;
    
    public IContentEntry CurrentEntry
    {
        get;
        set
        {
            if(ProcessContent(value))
                return;

            OnPropertyChanging();
            field = value;
            OnPropertyChanged();
            
            SearchText = value.FullPath.ToString();
            if (value.GetRoot() is ServerFolderContentEntry serverEntry)
            {
                ServerText = serverEntry.ServerUrl.ToString();
            }
        }
    }

    public void OnBackEnter()
    {
        if (CurrentEntry.Parent is null)
        {
            SetHubRoot();
            return;
        }
        CurrentEntry.Parent?.GoCurrent();
    }

    public void OnUnpack()
    {
        if(CurrentEntry is not ServerFolderContentEntry serverEntry) 
            return;

        serverEntry.UnpackServerFiles();
    }

    public async Task<IContentEntry?> GetEntry(ContentPath path)
    {
        var cur = ServiceProvider.GetService<ServerFolderContentEntry>()!;
        cur.Init(this, ServerText.ToRobustUrl());
        return await cur.Go(path, CancellationService.Token);
    }

    public async void OnGoEnter()
    {
        if (string.IsNullOrWhiteSpace(ServerText))
        {
            SetHubRoot();
            SearchText = string.Empty;
            return;
        }

        try
        {
            CurrentEntry = await GetEntry(new ContentPath(SearchText))?? throw new NullReferenceException($"{SearchText} not found in {ServerText}");
        }
        catch (Exception e)
        {
            PopupService.Popup(e);
            ServerText = string.Empty;
            SearchText = string.Empty;
            SetHubRoot();
        }
    }
    
    private bool ProcessContent(IContentEntry entry)
    {
        var ext = Path.GetExtension(entry.Name);
        
        if (entry is BaseFolderContentEntry dirEntry && ext == ".rsi")
        {
            Task.Run(async () =>
                CurrentEntry = await ViewHelperService.GetViewModel<RsiImageViewModel>().LoadFromDirectory(dirEntry));
            return true;
        }

        return false;
    }
    
    protected override void InitialiseInDesignMode()
    {
        var root = ViewHelperService.GetViewModel<FolderContentEntry>();
        root.Init(this);
        var child = root.AddFolder("Biba");
        child.AddFolder("Boba");
        child.AddFolder("Buba");
        CurrentEntry = root;
    }

    protected override void Initialise()
    {
        SetHubRoot();
    }

    public void SetHubRoot()
    {
        ServerText = string.Empty;
        SearchText = string.Empty;
        var root = ViewHelperService.GetViewModel<ServerListContentEntry>();
        root.InitHubList(this);
        CurrentEntry = root;
    }

    public void Go(RobustUrl url, ContentPath path)
    {
        ServerText = url.ToString();
        SearchText = path.ToString();
        OnGoEnter();
    }
}
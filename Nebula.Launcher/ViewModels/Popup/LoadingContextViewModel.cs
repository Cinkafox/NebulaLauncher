using System;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(LoadingContextView), false)]
[ConstructGenerator]
public sealed partial class LoadingContextViewModel : PopupViewModelBase
{
    public ObservableCollection<LoadingContextEntry> Entries { get; } = [];
    
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public CancellationService CancellationService { get; }
    
    private Lock _contextLock = new();

    public string LoadingName { get; set; } = LocalizationService.GetString("popup-loading");
    public bool IsCancellable { get; set; } = true;
    public override bool IsClosable => false;

    public override string Title => LocalizationService.GetString("popup-loading");

    public void Cancel()
    {
        if (!IsCancellable) return;
        CancellationService.Cancel();
        Dispose();
    }

    public LoadingContextEntry CreateContextEntry()
    {
        lock (_contextLock)
        {
            var instance = new LoadingContextEntry(this);
            Entries.Add(instance);
            return instance;
        }
    }

    public void RemoveLoadingContext(LoadingContextEntry entry)
    {
        lock (_contextLock)
        {
            Entries.Remove(entry);
        }
    }

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
        CreateTestEntry();
        CreateTestEntry();
        CreateTestEntry();
    }

    private void CreateTestEntry()
    {
        var entry = CreateContextEntry();
        
        var context = entry.CreateLoadingContext();
        context.SetJobsCount(5);
        context.SetResolvedJobsCount(2);
        context.SetLoadingMessage("message");

        var ctx1 = entry.CreateLoadingContext(new FileLoadingFormater());
        ctx1.SetJobsCount(1020120);
        ctx1.SetResolvedJobsCount(12331);
        ctx1.SetLoadingMessage("File data");
        
        for (var i = 0; i < 14; i++)
        {
            entry.PasteSpeed(Random.Shared.Next(10000000));
        }
    }
}

public sealed partial class LoadingContextEntry(LoadingContextViewModel mainModel) : 
    ObservableObject, 
    ILoadingHandlerFactory, 
    IConnectionSpeedHandler, ILoadingHandlerEntryFactory
{
    public ObservableCollection<double> Values { get; } = [];
    
    public ObservableCollection<LoadingContext> LoadingContexts { get;  } = [];
    private readonly Lock _contextLock = new();
    private readonly Lock _speedLock = new();
    
    [ObservableProperty] private string _speedText = "";
    [ObservableProperty] private bool _showSpeed;
    [ObservableProperty] private int _loadingColumnSize = 2;
    
    public void RemoveContextInstance(ILoadingHandler loadingContext)
    {
        if (loadingContext is not LoadingContext context) return;
        lock (_contextLock)
        {
            LoadingContexts.Remove(context);
        }
    }

    public ILoadingHandler CreateLoadingContext(ILoadingFormater? loadingFormater = null)
    {
        lock (_contextLock)
        {
            var instance = new LoadingContext(this, loadingFormater ?? DefaultLoadingFormater.Instance);
            LoadingContexts.Add(instance);
            return instance;
        }
    }

    public void PasteSpeed(int speed)
    {
        lock (_speedLock)
        {
            if (Values.Count == 0)
            {
                ShowSpeed = true;
                LoadingColumnSize = 1;
            }
            SpeedText = FileLoadingFormater.FormatBytes(speed) + " / s";
            Values.Add(speed);
            if(Values.Count > 10) Values.RemoveAt(0);
        }
    }
    
    public void Dispose()
    {
        mainModel.RemoveLoadingContext(this);
    }

    public ILoadingHandlerFactory CreateLoadingHandlerFactory()
    {
        return mainModel.CreateContextEntry();
    }
}

public sealed partial class LoadingContext : ObservableObject, ILoadingHandler
{
    private readonly ILoadingHandlerFactory _master;
    private readonly ILoadingFormater _loadingFormater;
    public string LoadingText => _loadingFormater.Format(this);
    
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private long _currJobs;
    [ObservableProperty] private long _resolvedJobs;

    public LoadingContext(ILoadingHandlerFactory master, ILoadingFormater loadingFormater)
    {
        _master = master;
        _loadingFormater = loadingFormater;
    }

    public void SetJobsCount(long count)
    {
        CurrJobs = count;
        OnPropertyChanged(nameof(LoadingText));
    }

    public long GetJobsCount()
    {
        return CurrJobs;
    }

    public void SetResolvedJobsCount(long count)
    {
        ResolvedJobs = count;
        OnPropertyChanged(nameof(LoadingText));
    }

    public long GetResolvedJobsCount()
    {
        return ResolvedJobs;
    }

    public void SetLoadingMessage(string message)
    {
        Message = message;
    }

    public void Dispose()
    {
        _master.RemoveContextInstance(this);
    }
}

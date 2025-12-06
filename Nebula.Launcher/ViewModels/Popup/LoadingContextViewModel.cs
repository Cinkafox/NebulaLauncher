using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(LoadingContextView), false)]
[ConstructGenerator]
public sealed partial class LoadingContextViewModel : PopupViewModelBase, ILoadingHandlerFactory, IConnectionSpeedHandler
{
    public ObservableCollection<LoadingContext> LoadingContexts { get;  } = [];
    public ObservableCollection<double> Values { get; } = [];
    [ObservableProperty] private string _speedText = "";
    [ObservableProperty] private bool _showSpeed;
    [ObservableProperty] private int _loadingColumnSize = 2;
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public CancellationService CancellationService { get; }

    public string LoadingName { get; set; } = LocalizationService.GetString("popup-loading");
    public bool IsCancellable { get; set; } = true;
    public override bool IsClosable => false;

    public override string Title => LoadingName;

    public void Cancel()
    {
        if (!IsCancellable) return;
        CancellationService.Cancel();
        Dispose();
    }

    public void PasteSpeed(int speed)
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

    public ILoadingHandler CreateLoadingContext(ILoadingFormater? loadingFormater = null)
    {
        var instance = new LoadingContext(this, loadingFormater ?? DefaultLoadingFormater.Instance);
        LoadingContexts.Add(instance);
        return instance;
    }

    public void RemoveContextInstance(LoadingContext loadingContext)
    {
        LoadingContexts.Remove(loadingContext);
        if(LoadingContexts.Count == 0) Dispose();
    }

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
        var context = CreateLoadingContext();
        context.SetJobsCount(5);
        context.SetResolvedJobsCount(2);
        context.SetLoadingMessage("message");

        var ctx1 = CreateLoadingContext(new FileLoadingFormater());
        ctx1.SetJobsCount(1020120);
        ctx1.SetResolvedJobsCount(12331);
        ctx1.SetLoadingMessage("File data");
        
        for (var i = 0; i < 14; i++)
        {
            PasteSpeed(Random.Shared.Next(10000000));
        }
    }
}

public sealed partial class LoadingContext : ObservableObject, ILoadingHandler
{
    private readonly LoadingContextViewModel _master;
    private readonly ILoadingFormater _loadingFormater;
    public string LoadingText => _loadingFormater.Format(this);
    
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private long _currJobs;
    [ObservableProperty] private long _resolvedJobs;

    public LoadingContext(LoadingContextViewModel master, ILoadingFormater loadingFormater)
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

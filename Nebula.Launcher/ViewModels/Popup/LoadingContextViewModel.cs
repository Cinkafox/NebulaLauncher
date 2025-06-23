using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(LoadingContextView), false)]
[ConstructGenerator]
public sealed partial class LoadingContextViewModel : PopupViewModelBase, ILoadingHandler
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public CancellationService CancellationService { get; }
    
    [ObservableProperty] private int _currJobs;

    [ObservableProperty] private int _resolvedJobs;

    public string LoadingName { get; set; } = LocalisationService.GetString("popup-loading");
    public bool IsCancellable { get; set; } = true;
    public override bool IsClosable => false;

    public override string Title => LoadingName;

    public void SetJobsCount(int count)
    {
        CurrJobs = count;
    }

    public int GetJobsCount()
    {
        return CurrJobs;
    }

    public void SetResolvedJobsCount(int count)
    {
        ResolvedJobs = count;
    }

    public int GetResolvedJobsCount()
    {
        return ResolvedJobs;
    }

    public void Cancel(){
        if(!IsCancellable) return;
        CancellationService.Cancel();
        Dispose();
    }

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
    }
}
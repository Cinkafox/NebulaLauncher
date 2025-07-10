using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(LoadingContextView), false)]
[ConstructGenerator]
public sealed partial class LoadingContextViewModel : PopupViewModelBase, ILoadingHandler
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public CancellationService CancellationService { get; }
    
    [ObservableProperty] private int _currJobs;
    [ObservableProperty] private int _resolvedJobs;
    [ObservableProperty] private string _message = string.Empty;

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

    public void SetLoadingMessage(string message)
    {
        Message = message + "\n" + Message;
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
        SetJobsCount(5);
        SetResolvedJobsCount(2);
        string[] debugMessages = {
            "Debug: Starting phase 1...",
            "Debug: Loading assets...",
            "Debug: Connecting to server...",
            "Debug: Fetching user data...",
            "Debug: Applying configurations...",
            "Debug: Starting phase 2...",
            "Debug: Rendering UI...",
            "Debug: Preparing scene...",
            "Debug: Initializing components...",
            "Debug: Running diagnostics...",
            "Debug: Checking dependencies...",
            "Debug: Verifying files...",
            "Debug: Cleaning up cache...",
            "Debug: Finalizing setup...",
            "Debug: Setup complete.",
            "Debug: Ready for launch."
        };

        foreach (string message in debugMessages)
        {
            SetLoadingMessage(message);
        }
    }
}
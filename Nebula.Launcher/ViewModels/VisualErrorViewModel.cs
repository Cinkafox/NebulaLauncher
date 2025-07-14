using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;


[ViewModelRegister(typeof(VisualErrorView))]
public partial class VisualErrorViewModel : ViewModelBase
{
    [ObservableProperty] private string _imgPath = "cinka";
    [ObservableProperty] private string _title = "Error";
    [ObservableProperty] private string _description = "This is an error.";
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}
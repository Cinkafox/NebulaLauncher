using Avalonia.Controls;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.Views.Pages;

public partial class ServerOverviewView : UserControl
{
    // This constructor is used when the view is created by the XAML Previewer
    public ServerOverviewView()
    {
        InitializeComponent();
        
        EssentialFilters.AddFilter("Non RP", "rp:none");
        EssentialFilters.AddFilter("Low RP", "rp:low");
        EssentialFilters.AddFilter("Medium RP", "rp:med");
        EssentialFilters.AddFilter("Hard RP", "rp:high");
        EssentialFilters.AddFilter("18+", "18+");
        
        LanguageFilters.AddFilter("RU","lang:ru");
        LanguageFilters.AddFilter("EN","lang:en");
    }

    // This constructor is used when the view is created via dependency injection
    public ServerOverviewView(ServerOverviewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
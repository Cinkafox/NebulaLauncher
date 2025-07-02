using Avalonia.Controls;

namespace Nebula.Launcher.Views.Pages;

public partial class ServerOverviewView : UserControl
{
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
}
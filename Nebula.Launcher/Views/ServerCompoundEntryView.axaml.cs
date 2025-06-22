using Avalonia.Controls;
using Nebula.Launcher.ViewModels;

namespace Nebula.Launcher.Views;

public partial class ServerCompoundEntryView : UserControl
{
    public ServerCompoundEntryView()
    {
        InitializeComponent();
    }

    public ServerCompoundEntryView(ServerCompoundEntryViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
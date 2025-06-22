using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nebula.Launcher.ViewModels.Popup;

namespace Nebula.Launcher.Views.Popup;

public partial class EditServerNameView : UserControl
{
    public EditServerNameView()
    {
        InitializeComponent();
    }

    public EditServerNameView(EditServerNameViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
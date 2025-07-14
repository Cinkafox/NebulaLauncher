using Avalonia.Controls;
using Nebula.Launcher.ViewModels;

namespace Nebula.Launcher.MessageBox;

public partial class MessageView : UserControl, IMessageContainerProvider
{
    private readonly VisualErrorViewModel _context;
    public MessageView()
    {
        InitializeComponent();
        _context = new VisualErrorViewModel();
        ErrorView.Content = _context;
    }

    public void ShowMessage(string message, string title)
    {
        _context.Title = title;
        _context.Description = message;
    }
}

public interface IMessageContainerProvider
{
    public void ShowMessage(string message, string title);
}
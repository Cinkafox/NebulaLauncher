using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Nebula.Launcher.MessageBox;

public partial class MessageWindow : Window, IMessageContainerProvider
{ 
    public MessageWindow()
    {
        InitializeComponent();
    }

    public void ShowMessage(string message, string title)
    {
        MessageView.ShowMessage(message, title);
    }
    
    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}
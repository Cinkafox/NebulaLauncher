using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Nebula.Launcher.ViewModels.Popup;

namespace Nebula.Launcher.Views.Popup;

public partial class TfaView : UserControl
{
    public List<TextBox> Boxes = new();
    
    public TfaView()
    {
        InitializeComponent();

        foreach (var textBox in TContainer.Children.Select(UnzipBox))
        {
            var currIndex = Boxes.Count;
            Boxes.Add(textBox);
            textBox.TextChanged += (_,_) => OnTextChanged(currIndex);
            textBox.PastingFromClipboard += OnPasteFromClipboard;
            textBox.KeyUp += (sender, args) =>
            {
                if (args.Key == Key.Back && string.IsNullOrEmpty(textBox.Text)) OnTextChanged(currIndex);
            };
            textBox.KeyDown += (sender, args) =>
            {
                textBox.Text = args.KeySymbol;
                textBox.SelectionStart = 1;
                //OnTextChanged(currIndex);
            };
        }
    }

    private void OnPasteFromClipboard(object? sender, RoutedEventArgs e)
    {
        // TODO: CLIPBOARD THINK
    }

    private void OnTextChanged(int index)
    {
        var box = Boxes[index];

        if (string.IsNullOrEmpty(box.Text))
        {
            if(index == 0) return;
            index--;
        }
        else
        {
            if(!int.TryParse(box.Text, out var _))
            {
                box.Text = "";
                return;
            }
            
            if (index == 5)
            {
                CheckupCode();
                return;
            }
            index++;
        }

        Boxes[index].Focus();
    }

    private void CheckupCode()
    {
        var str = "";
        foreach (var vtTextBox in Boxes)
        {
            if(string.IsNullOrEmpty(vtTextBox.Text)) return;
            str += vtTextBox.Text;
        }
        
        ((TfaViewModel)DataContext!).OnTfaEnter(str);
    }
    
    private TextBox UnzipBox(Control control)
    {
        var box = (Border)control;
        return (TextBox)box.Child!;
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        CheckupCode();
    }
}
using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Nebula.Launcher.Controls;

public class FilterBox : UserControl
{
    public static readonly StyledProperty<ICommand> FilterCommandProperty =
        AvaloniaProperty.Register<FilterBox, ICommand>(nameof(FilterCommand));

    public ICommand FilterCommand
    {
        get => GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }
    
    public Action<FilterBoxChangedEventArgs>? OnFilterChanged {get; set;}

    public string FilterBoxName {
        set => filterName.LocalId = value;
        get => filterName.LocalId;
    }
    
    private StackPanel filterPanel;
    private LocalizedLabel filterName = new LocalizedLabel()
    {
        VerticalAlignment = VerticalAlignment.Center
    };
    
    public FilterBox()
    {
        filterPanel = new StackPanel()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
        };

        filterPanel.Children.Add(filterName);
        
        Content = filterPanel;
    }

    public void AddFilter(string name, string tag)
    {
        var checkBox = new CheckBox();
        checkBox.Content = new TextBlock()
        {
            Text = name,
        };
        
        
        
        checkBox.IsCheckedChanged += (_, _) =>
        {
            var args = new FilterBoxChangedEventArgs(tag, checkBox.IsChecked ?? false);
            OnFilterChanged?.Invoke(args);
            FilterCommand?.Execute(args);
        };
        
        filterPanel.Children.Add(checkBox);
    }
}

public sealed class FilterBoxChangedEventArgs : EventArgs
{
    public FilterBoxChangedEventArgs(string name, bool @checked)
    {
        Tag = name;
        Checked = @checked;
    }

    public string Tag {get; private set;}
    public bool Checked {get; private set;}
}
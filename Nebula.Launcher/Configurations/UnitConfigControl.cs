using Avalonia.Controls;
using Avalonia.Layout;

namespace Nebula.Launcher.ViewModels.Pages;

public abstract class UnitConfigControl<T> : Border, IConfigControl where T : notnull
{
    private readonly Label _nameLabel = new();
    private readonly TextBox _valueLabel = new();
    private string _originalValue;

    private StackPanel _panel = new();
    
    public string ConfigName { get; }
   
    public bool Dirty => _originalValue != ConfigValue;

    protected string ConfigValue
    {
        get => _valueLabel.Text ?? string.Empty;
        set => _valueLabel.Text = value;
    }

    public UnitConfigControl(string name, T value)
    {
        Classes.Add("ConfigBorder");
        ConfigName = name;
        _panel.Orientation = Orientation.Horizontal;
        _panel.Children.Add(_nameLabel);
        _panel.Children.Add(_valueLabel);
        
        _nameLabel.Content = name;
        _nameLabel.VerticalAlignment = VerticalAlignment.Center;
        Child = _panel;
        
        SetConfValue(value);
        _originalValue = ConfigValue;
    }

    public abstract void SetConfValue(T value);

    public abstract T GetConfValue();
    
    public void SetValue(object value)
    {
        SetConfValue((T)value);
    }

    public object GetValue()
    {
        return GetConfValue()!;
    }
}
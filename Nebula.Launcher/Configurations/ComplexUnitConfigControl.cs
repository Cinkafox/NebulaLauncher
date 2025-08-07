using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Nebula.Launcher.ViewModels.Pages;

public sealed class ComplexUnitConfigControl : Border, IConfigControl
{
    private readonly List<(PropertyInfo, IConfigControl)> _units = [];
    
    private Type _objectType = typeof(object);

    private readonly StackPanel _panel = new();
    
    public string ConfigName { get; }
    public bool Dirty => _units.Any(dirty => dirty.Item2.Dirty);

    public ComplexUnitConfigControl(string name, object obj)
    {
        Classes.Add("ConfigBorder");
        _panel.Orientation = Orientation.Vertical;
        _panel.Spacing = 4f;
        ConfigName = name;
        Child = _panel;
        SetValue(obj);
    }

    public void SetValue(object value)
    {
        _units.Clear();
        _panel.Children.Clear();
        _objectType = value.GetType();
        
        _panel.Children.Add(new Label()
        {
            Content = ConfigName
        });
        
        foreach (var propInfo in _objectType.GetProperties())
        {
            if(propInfo.PropertyType.IsInterface) 
                continue;
            
            var propValue = propInfo.GetValue(value);

            var control = ConfigControlHelper.GetConfigControl(propInfo.Name, propValue!);
            
            ((Control)control).Margin = new Thickness(5);
            _panel.Children.Add((Control)control);
            _units.Add((propInfo,control));
        }
    }

    public object GetValue()
    {
        var obj = ConfigControlHelper.CreateDefaultValue(_objectType);
        foreach (var (fieldInfo, configControl) in _units)
        {
            fieldInfo.SetValue(obj, configControl.GetValue());
        }

        return obj!;
    }
}
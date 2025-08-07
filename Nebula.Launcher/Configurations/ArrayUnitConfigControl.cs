using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Nebula.Launcher.ViewModels.Pages;

public sealed class ArrayUnitConfigControl : Border, IConfigControl
{
    private readonly List<IConfigControl> _itemControls = [];
    private readonly StackPanel _itemsPanel = new StackPanel() { Orientation = Orientation.Vertical };
    private readonly Button _addButton = new Button() { Content = new Label()
    {
        Content = "Add Item"
    }, Classes = { "ConfigBorder" }};
    private readonly int _oldCount;
    private readonly Type _elementType;
    private readonly StackPanel _panel = new();

    public string ConfigName { get; }
    public bool Dirty => _itemControls.Any(dirty => dirty.Dirty) || _itemControls.Count != _oldCount;

    public ArrayUnitConfigControl(string name, object value)
    {
        Classes.Add("ConfigBorder");
        _elementType = value.GetType().GetElementType()!;
        
        ConfigName = name;
        _panel.Orientation = Orientation.Vertical;
        _panel.Spacing = 4f;
        _itemsPanel.Spacing = 4f;

        _panel.Children.Add(new Label { Content = name });
        _panel.Children.Add(_itemsPanel);
        _panel.Children.Add(_addButton);

        _addButton.Click += (_, _) => AddItem(ConfigControlHelper.CreateDefaultValue(_elementType)!);
        Child = _panel; 
        SetValue(value);
        _oldCount = _itemControls.Count;
    }

    private void AddItem(object value)
    {
        var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var control = ConfigControlHelper.GetConfigControl(_itemControls.Count.ToString(), value);
        var removeButton = new Button { Content = new Label(){ Content = "Remove" }, Classes = { "ConfigBorder" }};

        removeButton.Click += (_, _) =>
        {
            _itemControls.Remove(control);
            _itemsPanel.Children.Remove(itemPanel);
        };
        
        ((Control)control).Margin = new Thickness(5);
        itemPanel.Children.Add((Control)control);
        itemPanel.Children.Add(removeButton);

        _itemsPanel.Children.Add(itemPanel);
        _itemControls.Add(control);
    }

    public void SetValue(object value)
    {
        _itemControls.Clear();
        _itemsPanel.Children.Clear();

        if (value is IEnumerable list)
        {
            foreach (var item in list)
            {
                AddItem(item);
            }
        }
    }

    public object GetValue()
    {
        return ConvertArray(_itemControls.Select(c => c.GetValue()).ToArray(), _elementType);
    }
    
    public static Array ConvertArray(Array sourceArray, Type targetType)
    {
        int length = sourceArray.Length;
        var newArray = Array.CreateInstance(targetType, length);

        for (int i = 0; i < length; i++)
        {
            var value = sourceArray.GetValue(i);
            var converted = Convert.ChangeType(value, targetType);
            newArray.SetValue(converted, i);
        }

        return newArray;
    }
}
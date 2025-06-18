using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ConfigurationView))]
[ConstructGenerator]
public partial class ConfigurationViewModel : ViewModelBase
{
    public ObservableCollection<IConfigControl> ConfigurationVerbose { get; } = new();
    
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;

    public List<(object, Type)> ConVarList = new();

    public void AddCvarConf<T>(ConVar<T> cvar)
    {
        ConfigurationVerbose.Add(
            ConfigControlHelper.GetConfigControl(cvar.Name, ConfigurationService.GetConfigValue(cvar)));
        ConVarList.Add((cvar, cvar.Type));
    }

    public void InvokeUpdateConfiguration()
    {
        for (int i = 0; i < ConfigurationVerbose.Count; i++)
        {
            var conVarControl = ConfigurationVerbose[i];
            if(!conVarControl.Dirty) 
                continue;
            
            var conVar = ConVarList[i];
            var methodInfo = ConfigurationService.GetType().GetMethod("SetConfigValue")!.MakeGenericMethod(conVar.Item2);
            methodInfo.Invoke(ConfigurationService, [conVar.Item1, conVarControl.GetValue()]);
        }
    }
    
    protected override void InitialiseInDesignMode()
    { 
        AddCvarConf(LauncherConVar.ILSpyUrl);
        AddCvarConf(LauncherConVar.Hub);
        AddCvarConf(LauncherConVar.AuthServers);
        AddCvarConf(CurrentConVar.EngineManifestUrl);
        AddCvarConf(CurrentConVar.RobustAssemblyName);
        AddCvarConf(CurrentConVar.ManifestDownloadProtocolVersion);
    }

    protected override void Initialise()
    {
        InitialiseInDesignMode();
    }
}

public static class ConfigControlHelper{
    public static IConfigControl GetConfigControl(string name,object value)
    {
        switch (value)
        {
            case string stringValue:
                return new StringUnitConfigControl(name, stringValue);
            case int intValue:
                return new IntUnitConfigControl(name, intValue);
            case float floatValue:
                return new FloatUnitConfigControl(name, floatValue);
        }
        
        var valueType = value.GetType();

        if (valueType.IsArray)
            return new ArrayUnitConfigControl(name, value);
        
        return new ComplexUnitConfigControl(name, value);
    }

    public static object? CreateDefaultValue(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        if (type == typeof(int))
            return 0;
        if (type == typeof(float))
            return 0f;
        if(type.IsValueType)
            return Activator.CreateInstance(type);
        
        var ctor = type.GetConstructors().First();
        var parameters = ctor.GetParameters()
            .Select(p => CreateDefaultValue(p.ParameterType))
            .ToArray();
        
        return ctor.Invoke(parameters);
    }
}

public sealed class ComplexUnitConfigControl : StackPanel, IConfigControl
{
    private List<(PropertyInfo, IConfigControl)> _units = [];
    
    private Type _objectType = typeof(object);
    
    public string ConfigName { get; }
    public bool Dirty => _units.Any(dirty => dirty.Item2.Dirty);

    public ComplexUnitConfigControl(string name, object obj)
    {
        Orientation = Orientation.Vertical;
        Margin = new Thickness(5);
        Spacing = 2f;
        ConfigName = name;
        SetValue(obj);
    }

    public void SetValue(object value)
    {
        _units.Clear();
        Children.Clear();
        _objectType = value.GetType();
        
        Children.Add(new Label()
        {
            Content = ConfigName
        });
        
        foreach (var propInfo in _objectType.GetProperties())
        {
            if(propInfo.PropertyType.IsInterface) 
                continue;
            
            var propValue = propInfo.GetValue(value);

            var control = ConfigControlHelper.GetConfigControl(propInfo.Name, propValue);
            
            Children.Add(control as Control);
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

        return obj;
    }
}

public sealed class ArrayUnitConfigControl : StackPanel, IConfigControl
{
    private readonly List<IConfigControl> _itemControls = [];
    private readonly StackPanel _itemsPanel = new StackPanel() { Orientation = Orientation.Vertical };
    private readonly Button _addButton = new Button() { Content = "Add Item" };
    private int oldCount;
    private readonly Type _elementType;

    public string ConfigName { get; }
    public bool Dirty => _itemControls.Any(dirty => dirty.Dirty) || _itemControls.Count != oldCount;

    public ArrayUnitConfigControl(string name, object value)
    {
        _elementType = value.GetType().GetElementType();
        
        ConfigName = name;
        Orientation = Orientation.Vertical;
        Margin = new Thickness(5);
        Spacing = 2f;

        Children.Add(new Label { Content = name });
        Children.Add(_itemsPanel);
        Children.Add(_addButton);

        _addButton.Click += (_, _) => AddItem(ConfigControlHelper.CreateDefaultValue(_elementType));

        SetValue(value);
        oldCount = _itemControls.Count;
    }

    private void AddItem(object value)
    {
        var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var control = ConfigControlHelper.GetConfigControl(_itemControls.Count.ToString(), value);
        var removeButton = new Button { Content = "Remove" };

        removeButton.Click += (_, _) =>
        {
            _itemControls.Remove(control);
            _itemsPanel.Children.Remove(itemPanel);
        };

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

public abstract class UnitConfigControl<T> : StackPanel, IConfigControl where T : notnull
{
    private readonly Label _nameLabel = new Label();
    private readonly TextBox _valueLabel = new TextBox();
    private string _originalValue;
    
    public string ConfigName { get; private set;}
   
    public bool Dirty
    {
        get
        {
            return _originalValue != ConfigValue;
        }
    }

    protected string? ConfigValue
    {
        get => _valueLabel.Text;
        set => _valueLabel.Text = value;
    }

    public UnitConfigControl(string name, T value)
    {
        ConfigName = name;
        Orientation = Orientation.Horizontal;
        Children.Add(_nameLabel);
        Children.Add(_valueLabel);
        
        _nameLabel.Content = name;
        _nameLabel.VerticalAlignment = VerticalAlignment.Center;
        
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

public sealed class StringUnitConfigControl(string name, string value) : UnitConfigControl<string>(name, value)
{
    public override void SetConfValue(string value)
    {
        ConfigValue = value;
    }

    public override string GetConfValue()
    {
        return ConfigValue ?? string.Empty;
    }
}

public sealed class IntUnitConfigControl(string name, int value) : UnitConfigControl<int>(name, value)
{
    public override void SetConfValue(int value)
    {
        ConfigValue = value.ToString();
    }

    public override int GetConfValue()
    {
        Debug.Assert(ConfigValue != null, nameof(ConfigValue) + " != null");
        
        return int.Parse(ConfigValue);
    }
}

public sealed class FloatUnitConfigControl(string name, float value) : UnitConfigControl<float>(name, value)
{

    public CultureInfo CultureInfo = CultureInfo.InvariantCulture;

    public override void SetConfValue(float value)
    {
        ConfigValue = value.ToString(CultureInfo);
    }

    public override float GetConfValue()
    {
        Debug.Assert(ConfigValue != null, nameof(ConfigValue) + " != null");
        
        return float.Parse(ConfigValue, CultureInfo);
    }
}


public interface IConfigControl
{
    public string ConfigName { get; }
    public bool Dirty {get;}
    public abstract void SetValue(object value);
    public abstract object GetValue();
}
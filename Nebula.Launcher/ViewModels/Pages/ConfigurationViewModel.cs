using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Configurations;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ConfigurationView))]
[ConstructGenerator]
public partial class ConfigurationViewModel : ViewModelBase
{
    public ObservableCollection<IConfigControl> ConfigurationVerbose { get; } = new();
    
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] private FileService FileService { get; set; } = default!;
    [GenerateProperty] private ContentService ContentService { get; set; } = default!;
    [GenerateProperty] private CancellationService CancellationService { get; set; } = default!;
    [GenerateProperty] private ViewHelperService ViewHelperService { get; set; } = default!;


    private readonly List<(object, Type)> _conVarList = new();

    public void AddCvarConf<T>(ConVar<T> cvar)
    {
        ConfigurationVerbose.Add(
            ConfigControlHelper.GetConfigControl(cvar.Name, ConfigurationService.GetConfigValue(cvar)!));
        _conVarList.Add((cvar, cvar.Type));
    }

    public void InvokeUpdateConfiguration()
    {
        for (int i = 0; i < ConfigurationVerbose.Count; i++)
        {
            var conVarControl = ConfigurationVerbose[i];
            if(!conVarControl.Dirty) 
                continue;
            
            var conVar = _conVarList[i];
            var methodInfo = ConfigurationService.GetType().GetMethod("SetConfigValue")!.MakeGenericMethod(conVar.Item2);
            methodInfo.Invoke(ConfigurationService, [conVar.Item1, conVarControl.GetValue()]);
        }
    }

    public void ResetConfig()
    {
        foreach (var conVar in _conVarList)
        {
            var methodInfo = ConfigurationService.GetType().GetMethod("SetConfigValue")!.MakeGenericMethod(conVar.Item2);
            methodInfo.Invoke(ConfigurationService, [conVar.Item1, null]);
        }
        
        _conVarList.Clear();
        ConfigurationVerbose.Clear();

        InitConfiguration();
        
        PopupService.Popup("Configuration has been reset.");
    }

    public void OpenDataFolder()
    {
        ExplorerHelper.OpenFolder(FileService.RootPath);
    }

    public void ExportLogs()
    {
        var logPath = Path.Join(FileService.RootPath, "log");
        var path = Path.Combine(Path.GetTempPath(), "tempThink"+Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        
        ZipFile.CreateFromDirectory(logPath, Path.Join(path, DateTime.Now.ToString("yyyy-MM-dd") + ".zip"));
        ExplorerHelper.OpenFolder(path);
    }

    public void RemoveAllContent()
    {
        Task.Run(() =>
        {
            using var loader = ViewHelperService.GetViewModel<LoadingContextViewModel>();
            loader.LoadingName = "Removing content";
            PopupService.Popup(loader);
            ContentService.RemoveAllContent(loader, CancellationService.Token);
        });
    }

    private void InitConfiguration()
    {
        AddCvarConf(LauncherConVar.ILSpyUrl);
        AddCvarConf(LauncherConVar.Hub);
        AddCvarConf(LauncherConVar.AuthServers);
        AddCvarConf(CurrentConVar.EngineManifestUrl);
        AddCvarConf(CurrentConVar.RobustAssemblyName);
        AddCvarConf(CurrentConVar.ManifestDownloadProtocolVersion);
    }
    
    protected override void InitialiseInDesignMode()
    {
        InitConfiguration();
    }

    protected override void Initialise()
    {
        InitConfiguration();
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
        if(type.IsValueType)
            return Activator.CreateInstance(type);
        
        var ctor = type.GetConstructors().First();
        var parameters = ctor.GetParameters()
            .Select(p => CreateDefaultValue(p.ParameterType))
            .ToArray();
        
        return ctor.Invoke(parameters);
    }
}

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

public sealed class StringUnitConfigControl(string name, string value) : UnitConfigControl<string>(name, value)
{
    public override void SetConfValue(string value)
    {
        ConfigValue = value;
    }

    public override string GetConfValue()
    {
        return ConfigValue;
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
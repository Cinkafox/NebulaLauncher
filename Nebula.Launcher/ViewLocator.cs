using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        if (param is Exception e)
        {
            return new ExceptionView(e);
        }

        var type = param.GetType().GetCustomAttribute<ViewModelRegisterAttribute>()?.Type;

        if (type != null) return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + param.GetType() };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || data is Exception;
    }
}
using System;
using System.Linq;

namespace Nebula.Launcher.ViewModels.Pages;

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
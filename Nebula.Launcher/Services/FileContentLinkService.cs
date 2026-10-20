using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Nebula.Shared;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public class FileContentLinkService
{
    private readonly Dictionary<string, Type> _assignedFileContent = [];
    
    public void AssignAllExtensions()
    {
        _assignedFileContent.Clear();
        
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => p.IsDefined(typeof(FileContentAssignAttribute), false));

        foreach (var type in types)
        {
            var extensions = type.GetCustomAttribute<FileContentAssignAttribute>()!.Extension;
            foreach (var extension in extensions)
            {
                _assignedFileContent.Add(extension, type);
            }
        }
    }

    public bool TryGetFileContent(string extension,[NotNullWhen(true)] out Type? type)
    {
        return _assignedFileContent.TryGetValue(extension, out type);
    }

    public Type? GetFileContent(string extension)
    {
        return _assignedFileContent.GetValueOrDefault(extension);
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class FileContentAssignAttribute(string[] extension) : Attribute
{
    public string[] Extension { get; } = extension;
}
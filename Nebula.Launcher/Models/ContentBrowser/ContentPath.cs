using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Nebula.Launcher.Models.ContentBrowser;

public struct ContentPath : IEquatable<ContentPath>
{
    public static readonly ContentPath Empty = new();
    
    public List<string> Pathes { get; }

    public ContentPath()
    {
        Pathes = [];
    }

    public ContentPath(List<string> pathes)
    {
        Pathes = pathes;
    }

    public ContentPath(string? path)
    {
        Pathes = string.IsNullOrEmpty(path)
            ? new List<string>()
            : path.Split(['/'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public ContentPath With(string? name)
    {
        if (name != null) return new ContentPath([..Pathes, name]);
        return new ContentPath(Pathes);
    }

    public ContentPath GetDirectory()
    {
        if (Pathes.Count == 0)
            return this;

        var directoryPathes = Pathes.Take(Pathes.Count - 1).ToList();
        return new ContentPath(directoryPathes);
    }

    public string GetName()
    {
        if (Pathes.Count == 0)
            throw new InvalidOperationException("Cannot get the name of the root path.");

        return Pathes.Last();
    }

    public string GetNext()
    {
        if (Pathes.Count == 0)
            throw new InvalidOperationException("No elements left to retrieve from the root.");

        var nextName = Pathes[0];
        Pathes.RemoveAt(0);

        return string.IsNullOrWhiteSpace(nextName) ? GetNext() : nextName;
    }

    public bool TryNext([NotNullWhen(true)]out string? part)
    {
        part = null;
        if (Pathes.Count == 0) return false;
        part = GetNext();
        return true;
    }

    public ContentPath Clone()
    {
        return new ContentPath(new List<string>(Pathes));
    }

    public string Path => Pathes.Count == 0 ? "/" : string.Join("/", Pathes);

    public override string ToString()
    {
        return Path;
    }

    public bool IsEmpty()
    {
        return Pathes.Count == 0;
    }

    public bool Equals(ContentPath other)
    {
        return Pathes.Equals(other.Pathes);
    }

    public override bool Equals(object? obj)
    {
        return obj is ContentPath other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Pathes.GetHashCode();
    }
}
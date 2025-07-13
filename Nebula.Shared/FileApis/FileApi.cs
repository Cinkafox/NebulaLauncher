using System.Diagnostics.CodeAnalysis;
using Nebula.Shared.FileApis.Interfaces;

namespace Nebula.Shared.FileApis;

public sealed class FileApi : IReadWriteFileApi
{
    public string RootPath;

    public FileApi(string rootPath)
    {
        RootPath = rootPath;
    }

    public bool TryOpen(string path,[NotNullWhen(true)] out Stream? stream)
    {
        var fullPath = Path.Join(RootPath, path);
        if (File.Exists(fullPath))
            try
            {
                stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                stream = null;
                return false;
            }

        stream = null;
        return false;
    }

    public bool Save(string path, Stream input)
    {
        var currPath = Path.Join(RootPath, path);

        try
        {
            var dirInfo = new DirectoryInfo(Path.GetDirectoryName(currPath) ?? throw new InvalidOperationException());
            if (!dirInfo.Exists) dirInfo.Create();

            using var stream = new FileStream(currPath, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Remove(string path)
    {
        var fullPath = Path.Join(RootPath, path);
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
        }
        catch
        {
            // Log exception if necessary
        }

        return false;
    }

    public bool Has(string path)
    {
        var fullPath = Path.Join(RootPath, path);
        return File.Exists(fullPath);
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        return Directory.GetDirectories(Path.Join(RootPath, path)).Select(p=>p.Replace(RootPath,"").Substring(1));
    }

    private IEnumerable<string> GetAllFiles(){

        if(!Directory.Exists(RootPath)) return [];
        return Directory.EnumerateFiles(RootPath, "*.*", SearchOption.AllDirectories).Select(p=>p.Replace(RootPath,"").Substring(1));
    }

    public IEnumerable<string> AllFiles => GetAllFiles();
}
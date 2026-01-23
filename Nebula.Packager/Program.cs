using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nebula.Shared;
using Nebula.SharedModels;

namespace Nebula.Packager;
public static class Program
{
    public static void Main(string[] args)
    {
        var parsedArgs = CommandLineParser.Parse(args);
        
        Pack(parsedArgs.RootPath, parsedArgs.Configuration);
    }

    private static string ShowEmptyOrValue(string? value)
    {
        if(string.IsNullOrWhiteSpace(value)) return "<empty>";
        return value;
    }
    
    private static void Pack(string rootPath, string configuration)
    {
        Console.WriteLine($"Packaging with arguments: RootPath {ShowEmptyOrValue(rootPath)} and Configuration {configuration}");
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "publish",
                Path.Combine(rootPath, "Nebula.Launcher", "Nebula.Launcher.csproj"),
                "-c", configuration,
            }
        };
        
        var process = Process.Start(processInfo)!;
        process.WaitForExit();
        if(process.ExitCode != 0) 
            throw new Exception($"Packager has exited with code {process.ExitCode}");
        
        var destinationDirectory = Path.Combine(rootPath,"release");
        var sourceDirectory = Path.Combine("Nebula.Launcher", "bin", configuration,"publish");

        if (Directory.Exists(destinationDirectory))
        {
            Directory.Delete(destinationDirectory, true);
        }
        
        Directory.CreateDirectory(destinationDirectory);
        
        HashSet<LauncherManifestEntry> entries = new HashSet<LauncherManifestEntry>();
        
        foreach (var fileName in Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(fileName);
            
            var hash = md5.ComputeHash(stream);
            var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            
            if(!File.Exists(Path.Combine(destinationDirectory, hashStr)))
                File.Copy(fileName, Path.Combine(destinationDirectory, hashStr));
            
            var fileNameCut = fileName.Remove(0, sourceDirectory.Length + 1);
            
            entries.Add(new LauncherManifestEntry(hashStr, fileNameCut));
            Console.WriteLine($"Added {hashStr} file name {fileNameCut}");
        }

        var manifestRuntimeInfo = new LauncherRuntimeInfo(
            CurrentConVar.DotnetVersion.DefaultValue!,
            CurrentConVar.DotnetUrl.DefaultValue!
            );
        
        using var manifest = File.CreateText(Path.Combine(destinationDirectory, "manifest.json"));
        manifest.AutoFlush = true;
        manifest.Write(JsonSerializer.Serialize(new LauncherManifest(entries, manifestRuntimeInfo)));
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Nebula.SharedModels;
using Nebula.UpdateResolver.Configuration;
using Nebula.UpdateResolver.Rest;

namespace Nebula.UpdateResolver;

public partial class MainWindow : Window
{
    public static readonly string RootPath = AppDataPath.GetAppDataPath("Datum");
    
    private readonly HttpClient _httpClient = new();
    private readonly FileApi _fileApi = new(Path.Join(RootPath, "app"));
    private string _logStr = "";
    
    public MainWindow()
    {
        InitializeComponent();
        LogStandalone.OnLog += (message, percentage) =>
        {
            var percentText = "";
            if (percentage != 0)
                percentText = $"{percentage}%";
            
            Dispatcher.UIThread.Invoke(() =>
            {
                ProgressLabel.Content = message;
                PercentLabel.Content = percentText;
            });
            
            var messageOut =
                $"[{DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}]: {message} {percentText}";
            Console.WriteLine(messageOut);
            _logStr += messageOut + "\n";
        };
        
        LogStandalone.Log("Starting up");
        if (!Design.IsDesignMode)
            Task.Run(Start);
        else
            LogStandalone.Log("Debug information", 51);
    }

    private async Task Start()
    {
        try
        {
            var manifest = await RestStandalone.GetAsync<LauncherManifest>(
                new Uri(ConfigurationStandalone.GetConfigValue(UpdateConVars.UpdateCacheUrl)! + "/manifest.json"), CancellationToken.None);
            
            var info = EnsureFiles(FilterEntries(manifest.Entries));
            
            LogStandalone.Log("Downloading files...");

            foreach (var file in info.ToDelete)
            {
                LogStandalone.Log("Deleting " + file.Path);
                _fileApi.Remove(file.Path);
            }

            var loadedManifest = info.FilesExist;
            Save(loadedManifest, manifest.RuntimeInfo);

            var count = info.ToDownload.Count;
            var resolved = 0;

            foreach (var file in info.ToDownload)
            {
                using var response = await _httpClient.GetAsync(
                    ConfigurationStandalone.GetConfigValue(UpdateConVars.UpdateCacheUrl)
                    + "/" + file.Hash);

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                _fileApi.Save(file.Path, stream);
                resolved++;
                LogStandalone.Log("Saving " + file.Path, (int)(resolved / (float)count * 100f));

                loadedManifest.Add(file);
                Save(loadedManifest, manifest.RuntimeInfo);
            }

            LogStandalone.Log("Download finished. Running launcher...");

            await DotnetStandalone.Run(manifest.RuntimeInfo, Path.Join(_fileApi.RootPath, "Nebula.Launcher.dll"));
        }
        catch(HttpRequestException e){
            LogStandalone.LogError(e);
            LogStandalone.Log("Network connection error...");
            var logPath = Path.Join(RootPath,"updateResloverError.txt");
            await File.WriteAllTextAsync(logPath, _logStr);
            Process.Start(new ProcessStartInfo(){
                FileName = "notepad",
                Arguments = logPath
            });
        }
        catch (Exception e)
        {
            LogStandalone.LogError(e);
            var logPath = Path.Join(RootPath,"updateResloverError.txt");
            await File.WriteAllTextAsync(logPath, _logStr);
            Process.Start(new ProcessStartInfo(){
                FileName = "notepad",
                Arguments = logPath
            });
        }
        
        Thread.Sleep(4000);
        
        Environment.Exit(0);
    }

    private ManifestEnsureInfo EnsureFiles(HashSet<LauncherManifestEntry> entries)
    {
        LogStandalone.Log("Ensuring launcher manifest...");
        
        var toDownload = new HashSet<LauncherManifestEntry>();
        var toDelete = new HashSet<LauncherManifestEntry>();
        var filesExist = new HashSet<LauncherManifestEntry>();
        
        LogStandalone.Log("Manifest loaded!");
        if (ConfigurationStandalone.TryGetConfigValue(UpdateConVars.CurrentLauncherManifest, out var currentManifest))
        {
            LogStandalone.Log("Delta manifest loaded!");
            foreach (var file in currentManifest.Entries)
            {
                if (!entries.Contains(file))
                    toDelete.Add(EnsurePath(file));
                else
                    filesExist.Add(EnsurePath(file));
            }

            foreach (var file in entries)
            {
                if(!currentManifest.Entries.Contains(file))
                    toDownload.Add(EnsurePath(file));
            }
        }
        else
        {
            toDownload = entries;
        }
        
        LogStandalone.Log("Saving launcher manifest...");

        return new ManifestEnsureInfo(toDownload, toDelete, filesExist);
    }

    private HashSet<LauncherManifestEntry> FilterEntries(IEnumerable<LauncherManifestEntry> entries)
    {
        var filtered = new HashSet<LauncherManifestEntry>();
        var runtimeIdentifier = DotnetUrlHelper.GetRuntimeIdentifier();

        foreach (var entry in entries)
        {
            var splited = entry.Path.Split("/");
            
            if(splited.Length < 2 || 
               splited[0] != "runtimes" || 
               splited[1] == runtimeIdentifier)
            {
                filtered.Add(entry);
            }
        }
        
        return filtered;
    }

    private void Save(HashSet<LauncherManifestEntry> entries, LauncherRuntimeInfo info)
    {
        ConfigurationStandalone.SetConfigValue(UpdateConVars.CurrentLauncherManifest, new LauncherManifest(entries, info));
    }

    private LauncherManifestEntry EnsurePath(LauncherManifestEntry entry)
    {
        if(!PathValidator.IsSafePath(_fileApi.RootPath, entry.Path)) 
            throw new ArgumentException("Path contains invalid characters. Manifest hash: " + entry.Hash);

        return entry;
    }
}

public static class PathValidator
{
    public static bool IsSafePath(string baseDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return false;
        
        var fullBase = Path.GetFullPath(baseDirectory);

      
        var combinedPath = Path.Combine(fullBase, relativePath);
        var fullPath = Path.GetFullPath(combinedPath);


        if (!fullPath.StartsWith(fullBase, StringComparison.Ordinal))
            return false;
        
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            FileInfo fileInfo = new FileInfo(fullPath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;
        }

        return true;
    }
}
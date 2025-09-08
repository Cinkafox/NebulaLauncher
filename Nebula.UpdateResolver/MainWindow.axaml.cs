using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Nebula.UpdateResolver.Configuration;
using Nebula.UpdateResolver.Rest;

namespace Nebula.UpdateResolver;

public partial class MainWindow : Window
{
    public static readonly string RootPath = Path.Join(Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData), "Datum");
    
    private readonly HttpClient _httpClient = new HttpClient();
    public readonly FileApi FileApi = new FileApi(Path.Join(RootPath,"app"));
    private string LogStr = "";
    public MainWindow()
    {
        InitializeComponent();
        LogStandalone.OnLog += (message, percentage) =>
        {
            ProgressLabel.Content = message;
            if (percentage == 0)
                PercentLabel.Content = "";
            else
                PercentLabel.Content = percentage + "%";

            var messageOut =
                $"[{DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}]: {message} {PercentLabel.Content}";
            Console.WriteLine(messageOut);
            LogStr += messageOut + "\n";
        };
        if (!Design.IsDesignMode)
            Task.Run(()=> Start());
        else
            LogStandalone.Log("Debug information", 51);
    }

    private async Task Start()
    {
        try
        {
            var info = await EnsureFiles();
            LogStandalone.Log("Downloading files...");

            foreach (var file in info.ToDelete)
            {
                LogStandalone.Log("Deleting " + file.Path);
                FileApi.Remove(file.Path);
            }

            var loadedManifest = info.FilesExist;
            Save(loadedManifest);

            var count = info.ToDownload.Count;
            var resolved = 0;

            foreach (var file in info.ToDownload)
            {
                using var response = await _httpClient.GetAsync(
                    ConfigurationStandalone.GetConfigValue(UpdateConVars.UpdateCacheUrl)
                    + "/" + file.Hash);

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                FileApi.Save(file.Path, stream);
                resolved++;
                LogStandalone.Log("Saving " + file.Path, (int)(resolved / (float)count * 100f));

                loadedManifest.Add(file);
                Save(loadedManifest);
            }

            LogStandalone.Log("Download finished. Running launcher...");

            await DotnetStandalone.Run(Path.Join(FileApi.RootPath, "Nebula.Launcher.dll"));
        }
        catch(HttpRequestException e){
            LogStandalone.LogError(e);
            LogStandalone.Log("Network connection error...");
            var logPath = Path.Join(RootPath,"updateResloverError.txt");
            await File.WriteAllTextAsync(logPath, LogStr);
            Process.Start(new ProcessStartInfo(){
                FileName = "notepad",
                Arguments = logPath
            });
        }
        catch (Exception e)
        {
            LogStandalone.LogError(e);
            var logPath = Path.Join(RootPath,"updateResloverError.txt");
            await File.WriteAllTextAsync(logPath, LogStr);
            Process.Start(new ProcessStartInfo(){
                FileName = "notepad",
                Arguments = logPath
            });
        }
        
        Thread.Sleep(4000);
        
        Environment.Exit(0);
    }

    private async Task<ManifestEnsureInfo> EnsureFiles()
    {
        LogStandalone.Log("Ensuring launcher manifest...");
        var manifest = await RestStandalone.GetAsync<LauncherManifest>(
            new Uri(ConfigurationStandalone.GetConfigValue(UpdateConVars.UpdateCacheUrl)! + "/manifest.json"), CancellationToken.None);
        
        var toDownload = new HashSet<LauncherManifestEntry>();
        var toDelete = new HashSet<LauncherManifestEntry>();
        var filesExist = new HashSet<LauncherManifestEntry>();
        
        LogStandalone.Log("Manifest loaded!");
        if (ConfigurationStandalone.TryGetConfigValue(UpdateConVars.CurrentLauncherManifest, out var currentManifest))
        {
            LogStandalone.Log("Delta manifest loaded!");
            foreach (var file in currentManifest.Entries)
            {
                if (!manifest.Entries.Contains(file))
                    toDelete.Add(EnsurePath(file));
                else
                    filesExist.Add(EnsurePath(file));
            }

            foreach (var file in manifest.Entries)
            {
                if(!currentManifest.Entries.Contains(file))
                    toDownload.Add(EnsurePath(file));
            }
        }
        else
        {
            toDownload = manifest.Entries;
        }
        
        LogStandalone.Log("Saving launcher manifest...");

        return new ManifestEnsureInfo(toDownload, toDelete, filesExist);
    }
    

    private void Save(HashSet<LauncherManifestEntry> entries)
    {
        ConfigurationStandalone.SetConfigValue(UpdateConVars.CurrentLauncherManifest, new LauncherManifest(entries));
    }

    private LauncherManifestEntry EnsurePath(LauncherManifestEntry entry)
    {
        if(!PathValidator.IsSafePath(FileApi.RootPath, entry.Path)) 
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
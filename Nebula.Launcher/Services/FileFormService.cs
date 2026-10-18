using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Nebula.Shared;
using Nebula.Shared.FileApis;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public sealed partial class FileFormService
{
    private Window? _mainWindow;

    public void SetMainWindow(Window mainWindow)
    {
        if(_mainWindow != null)
            throw new Exception("Window is already set");
        
        _mainWindow = mainWindow;
    }
    public async Task<FileApi?> ShowOpenFileDialogAsync(string? title = null)
    {
        if(_mainWindow is null) return null;
        
        var storage = await _mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = false,
            Title = LocalizationService.GetString(title ?? "folder-form-select")
        });

        if (storage.Count == 0) return null;
        
        return new FileApi(storage[0].Path.LocalPath);
    }
}
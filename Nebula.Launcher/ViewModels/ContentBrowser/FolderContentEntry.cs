using Nebula.Launcher.Services;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class FolderContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    
    public FolderContentEntry AddFolder(string folderName)
    {
        var folder = ViewHelperService.GetViewModel<FolderContentEntry>();
        folder.Init(Holder, folderName);
        return AddChild(folder);
    }

    protected override void InitialiseInDesignMode() { }
    protected override void Initialise() { }
}
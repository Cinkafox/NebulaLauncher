using System.IO;
using System.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.ContentBrowser;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(Views.StaticImageView), false)]
[FileContentAssign([".png", ".jpeg"])]
public sealed partial class StaticImageViewModel : BaseFileContentEntry
{
    [ObservableProperty] private Bitmap? _image;
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public StaticImageViewModel LoadFromStream(Stream stream)
    {
        Image = new Bitmap(stream);
        return this;
    }

    public override async void Execute(CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenFile(cancellationToken);
        LoadFromStream(stream);
    }
}
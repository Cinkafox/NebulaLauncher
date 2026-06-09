using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(Views.StaticImageView), false)]
public sealed partial class StaticImageViewModel : ViewModelBase, IImageInput
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
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataExtractor;
using MetadataExtractor.Formats.Png;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;
using SkiaSharp;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(RsicShowView))]
public sealed partial class RsicShowViewModel : PopupViewModelBase
{
    public override string Title => LocalizationService.GetString("popup-rsic-show");
    public override bool IsClosable => true;
    
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    
    [ObservableProperty] private Bitmap? _image;

    private SKBitmap? _originalImage;
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private RsiJsonMetadata _currentRsi;
    [ObservableProperty] private RsiStateSelected _selectedState;
    [ObservableProperty] private int _rotation = 0;

    public ObservableCollection<RsiStateSelected> States { get; } = [];

    public void SetImage(Stream stream, bool readMetadata = false)
    {
        _originalImage?.Dispose();
        
        ShowSettings = false;
        
        if(readMetadata)
        {
            CurrentRsi = GetRsiMetadata(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            if (CurrentRsi is not null)
            {
                _originalImage = SKBitmap.Decode(stream);
                ShowSettings = true;
                States.Clear();

                for (var i = 0; i < CurrentRsi.States.Length; i++)
                {
                    var currState = CurrentRsi.States[i];
                    var index = i;

                    if (currState.Directions is not null)
                    {
                        if (currState.Delays is not null)
                        {
                            foreach (var delay in currState.Delays)
                            {
                                index += currState.Directions.Value * delay.Length;
                            }

                            index -= 1;
                        }
                        else
                        {
                            index += currState.Directions.Value - 1;
                        }
                    }
                    
                    States.Add(new RsiStateSelected(index, currState));
                }
                
                SelectedState = States[0];
            }
            
            return;
        }
        
        Image = new Bitmap(stream);
    }

    public void ChangeRotationLeft()
    {
        Rotation += 1;
    }
    
    public void ChangeRotationRight()
    {
        Rotation -= 1;
    }

    partial void OnRotationChanged(int value)
    {
        OnSelectedStateChanged(SelectedState);
    }

    partial void OnSelectedStateChanged(RsiStateSelected value)
    {
        if (_originalImage == null)
            return;

        _rotation = value.State.Directions != null 
            ? Math.Clamp(_rotation, 0, value.State.Directions.Value - 1) 
            : 0;

        using var pixmap =  new SKPixmap(_originalImage.Info, _originalImage.GetPixels());
        var (x,y) = GetCropPosition(
            _originalImage.Width, 
            _originalImage.Height,
            _currentRsi.Size.X, 
            _currentRsi.Size.Y, 
            value.Number + _rotation);
        
        var rectI = SKRectI.Create(x, y, _currentRsi.Size.X, _currentRsi.Size.Y);
        var subset = pixmap.ExtractSubset(rectI);
        Image = ConvertToAvaloniaBitmap(subset ?? throw new Exception());
    }
    
    private static (int X, int Y) GetCropPosition(
        int imageWidth, 
        int imageHeight, 
        int stateWidth, 
        int stateHeight, 
        int stateIndex)
    {
        if (stateWidth <= 0 || stateHeight <= 0)
            throw new ArgumentException("State width and height must be greater than zero.", nameof(stateWidth));
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentException("Image width and height must be greater than zero.");
        if (imageWidth < stateWidth || imageHeight < stateHeight)
            throw new ArgumentException("Image dimensions cannot be smaller than state dimensions.");

        var cols = imageWidth / stateWidth;
        var totalStates = cols * (imageHeight / stateHeight);

        if (stateIndex < 0 || stateIndex >= totalStates)
            throw new ArgumentOutOfRangeException(nameof(stateIndex), 
                $"State index must be between 0 and {totalStates - 1}.");

        var x = (stateIndex % cols) * stateWidth;
        var y = (stateIndex / cols) * stateHeight;

        return (x, y);
    }

    private Bitmap ConvertToAvaloniaBitmap(SKPixmap skBitmap)
    {
        using (var image = SKImage.FromPixels(skBitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = new MemoryStream())
        {
            data.SaveTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return new Bitmap(stream);
        }
    }

    private RsiJsonMetadata? GetRsiMetadata(Stream stream)
    {
        var directories = PngMetadataReader.ReadMetadata(stream);
        foreach (var directory in directories)
        {
            var textData = directory.GetObject(PngDirectory.TagTextualData);
            if (textData is not List<MetadataExtractor.KeyValuePair> valuePairs) continue;
            
            var manifest = valuePairs
                .Where(kvp => kvp.Key == "robusttoolbox_rsic_meta")
                .Select(kvp => kvp.Value.ToString()).FirstOrDefault();
            if(manifest is null) 
                continue;
            
            return JsonSerializer.Deserialize<RsiJsonMetadata>(manifest, SerializerOptions);
        }
        
        return null;
    }
    
    protected override void InitialiseInDesignMode()
    {
        
    }

    protected override void Initialise()
    {
    }
    
    protected override void OnDispose()
    {
        base.OnDispose();
        _originalImage?.Dispose();
    }
}

public record struct RsiStateSelected(int Number, StateJsonMetadata State);
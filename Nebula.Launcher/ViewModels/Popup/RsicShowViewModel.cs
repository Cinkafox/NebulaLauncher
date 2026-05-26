using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataExtractor.Formats.Png;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.ViewHelper;
using SkiaSharp;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(RsicShowView))]
public sealed partial class RsicShowViewModel : PopupViewModelBase
{
    public override string Title => LocalizationService.GetString("popup-rsic-show");
    public override bool IsClosable => true;
    
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public DebugService DebugService { get; }
    
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    
    [ObservableProperty] private Bitmap? _image;

    private SKBitmap? _originalImage;
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private RsiJsonMetadata _currentRsi;
    [ObservableProperty] private RsiStateSelected _selectedState;
    [ObservableProperty] private int _rotation = 0;
    [ObservableProperty] private int _frame = 0;
    
    private ILogger _logger;

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
                
                var startIndex = 0;

                foreach (var currState in CurrentRsi.States)
                {
                    var containIndexes = 0;
                    var direction = 1;
                    
                    if (currState.Directions is not null)
                        direction = currState.Directions.Value;
                    
                    if (currState.Delays is not null)
                    {
                        for (var d = 0; d < direction; d++)
                        {
                            containIndexes += currState.Delays[d].Length;
                        }
                    }
                    else
                    {
                        containIndexes += direction;
                    }
                    
                    States.Add(new RsiStateSelected(startIndex, currState));

                    startIndex += containIndexes;
                }
                
                SelectedState = States[0];
                
                return;
            }
        }
        
        Image = new Bitmap(stream);
    }

    public void ChangeRotationLeft()
    {
        if(SelectedState.State.Directions is null) 
            return;
        
        Rotation = (Rotation + 1) % SelectedState.State.Directions.Value;
    }
    
    public void ChangeRotationRight()
    {
        if(SelectedState.State.Directions is null) 
            return;
        
        Rotation = (Rotation + SelectedState.State.Directions.Value - 1) % SelectedState.State.Directions.Value;
    }

    public void NextFrame()
    {
        if(SelectedState.State.Delays?[Rotation] is null)
            return;

        Frame = (Frame + 1) % SelectedState.State.Delays[Rotation].Length;
    }

    partial void OnRotationChanged(int value)
    {
        OnSelectedStateChanged(SelectedState);
    }
    
    partial void OnFrameChanged(int value)
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
        
        _frame = value.State.Delays != null ? Math.Clamp(_frame, 0, value.State.Delays[_rotation].Length) : 0;
        
        var rotationFrameShift = 0;
        
        for (var i = 0; i < _rotation; i++)
        {
            if(value.State.Delays is null)
            {
                rotationFrameShift++;
                continue;
            }

            rotationFrameShift += value.State.Delays[i].Length;
        }

        using var pixmap = new SKPixmap(_originalImage.Info, _originalImage.GetPixels());
        var (x,y) = GetCropPosition(
            _originalImage.Width, 
            _originalImage.Height,
            _currentRsi.Size.X, 
            _currentRsi.Size.Y, 
            value.StartIndex + rotationFrameShift + _frame);
        
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
            
            if (textData is not List<MetadataExtractor.KeyValuePair> valuePairs)
            {
                if(textData is not MetadataExtractor.KeyValuePair[] valueArray)
                    continue;

                valuePairs = valueArray.ToList();
            }
            
            var manifest = valuePairs
                .Where(kvp => kvp.Key == "robusttoolbox_rsic_meta")
                .Select(kvp => kvp.Value.ToString()).FirstOrDefault();
            
            if(manifest is null)
                continue;
            
            return JsonSerializer.Deserialize<RsiJsonMetadata>(manifest, SerializerOptions);
        }
        
        _logger.Error("Manifest not found.");
        
        return null;
    }
    
    protected override void InitialiseInDesignMode()
    {
        
    }

    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
    }
    
    protected override void OnDispose()
    {
        base.OnDispose();
        _originalImage?.Dispose();
    }
}

public record struct RsiStateSelected(int StartIndex, StateJsonMetadata State);
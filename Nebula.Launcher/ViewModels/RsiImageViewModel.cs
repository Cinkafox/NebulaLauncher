using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Labs.Gif;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataExtractor.Formats.Png;
using Nebula.Launcher.Models;
using Nebula.Launcher.Models.ContentBrowser;
using Nebula.Launcher.Services;
using Nebula.Launcher.Utils;
using Nebula.Launcher.ViewModels.ContentBrowser;
using Nebula.Shared.ViewHelper;
using SkiaSharp;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(Views.RsiImageView), false)]
[FileContentAssign([".rsic"])]
public sealed partial class RsiImageViewModel : BaseFileContentEntry
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    
    [ObservableProperty] private string? _loadingErrorMessage;
    [ObservableProperty] private string _selectedState = "";
    [ObservableProperty] private int _selectedRotation;

    public GifStreamSource SelectedGif
    {
        get
        {
            if (!States.TryGetValue(SelectedState, out var rotArray) ||
                !rotArray.TryGetValue(SelectedRotation, out var rot))
            {
                Console.WriteLine($"{SelectedState} with rot {SelectedRotation} not found");
                return GifStreamSource.FromUri(new Uri($"avares://Nebula.Launcher/Assets/gif/back.gif"));
            }
            
            return GifStreamSource.FromStream(new MemoryStream(rot));
        }
    }

    public Dictionary<string, Dictionary<int, byte[]>> States { get; } = new();
    
    protected override void InitialiseInDesignMode() { }

    protected override void Initialise() { }

    public async Task<RsiImageViewModel> LoadFromDirectory(BaseFolderContentEntry entry)
    {
        Parent = entry.Parent;
        
        if(!entry.TryOpenFile<BaseFileContentEntry>("meta.json", out var file))
        {
            Console.WriteLine("RSI NOT FOUND");
            return this;
        }
        
        await using var stream = await file.OpenFile();
        var currentRsi = JsonSerializer.Deserialize<RsiJsonMetadata>(stream, SerializerOptions);
        
        if(currentRsi is null)
        {
            Console.WriteLine("RSI LOAD FAILED");
            return this;
        }
        
        States.Clear();

        foreach (var currState in currentRsi.States)
        {
            if (!States.TryGetValue(currState.Name, out var stateRotationDict))
            {
                stateRotationDict = [];
                States.Add(currState.Name, stateRotationDict);
            }
            
            if(!entry.TryOpenFile<BaseFileContentEntry>($"{currState.Name}.png", out var imageFile))
                return this;
            
            await using var imageStream = await imageFile.OpenFile();
            var image = SKBitmap.Decode(imageStream);
            var directionCount = currState.Directions ?? 1;

            for (var curRotation = 0; curRotation < directionCount; curRotation++)
            {
                stateRotationDict[curRotation] = ProcessAtlasImage(image, currentRsi, currState, curRotation, 0);
            }
        }
        
        SelectedState = currentRsi.States[0].Name;
        return this;
    }

    public RsiImageViewModel LoadFromStream(Stream stream)
    {
        var currentRsi = GetRsiMetadata(stream);
        stream.Seek(0, SeekOrigin.Begin);

        if (currentRsi is null || currentRsi.States.Length == 0)
            return this;
        
        var originalImage = SKBitmap.Decode(stream);
        States.Clear();

        var shift = 0;

        foreach (var currState in currentRsi.States)
        {
            if (!States.TryGetValue(currState.Name, out var stateRotationDict))
            {
                stateRotationDict = [];
                States.Add(currState.Name, stateRotationDict);
            }
            
            var directionCount = currState.Directions ?? 1;

            for (var curRotation = 0; curRotation < directionCount; curRotation++)
            {
                stateRotationDict[curRotation] = ProcessAtlasImage(originalImage, currentRsi, currState, curRotation, shift);
            }
            
            var containIndexes = 0;
            if (currState.Delays is not null)
            {
                for (var d = 0; d < directionCount; d++)
                {
                    containIndexes += currState.Delays[d].Length;
                }
            }
            else
            {
                containIndexes += directionCount;
            }
        
            shift = shift + containIndexes;
        }
        
        SelectedState = currentRsi.States[0].Name;
        return this;
    }

    partial void OnSelectedRotationChanged(int value)
    {
        if(string.IsNullOrEmpty(SelectedState)) return;
        OnPropertyChanged(nameof(SelectedGif));
    }

    partial void OnSelectedStateChanged(string value)
    {
        if(string.IsNullOrEmpty(SelectedState)) return;
        _selectedRotation = 0;
        OnPropertyChanged(nameof(SelectedGif));
    }

    public void ChangeRotationLeft()
    {
        if (!States.TryGetValue(SelectedState, out var rotArray)) return;
        SelectedRotation = (SelectedRotation + 1) % rotArray.Count;
    }
    
    public void ChangeRotationRight()
    {
        if (!States.TryGetValue(SelectedState, out var rotArray)) return;
        SelectedRotation = (SelectedRotation + rotArray.Count - 1) % rotArray.Count;
    }

  
    private byte[] ProcessAtlasImage(SKBitmap originalImage,
        RsiJsonMetadata currentRsi,
        StateJsonMetadata state,
        int rotation, 
        int shift)
    {
        rotation = state.Directions != null 
            ? Math.Clamp(rotation, 0, state.Directions.Value - 1) 
            : 0;
        
        var frameCount = state.Delays is null ? 1 : state.Delays[rotation].Length;
        var frameWidth = currentRsi.Size.X;
        var frameHeight = currentRsi.Size.Y;

        var encoder = new CustomGifEncoder();
        var rotationFrameShift = 0;
        for (var i = 0; i < rotation; i++)
        {
            if(state.Delays is null)
            {
                rotationFrameShift++;
                continue;
            }
            rotationFrameShift += state.Delays[i].Length;
        }

        for (var frame = 0; frame < frameCount; frame++)
        {
            var currDelay = state.Delays is not null ? state.Delays[rotation][frame] : 1f;
            var delayMs = Math.Max(1, (int)(currDelay * 100));

            var (x, y) = GetCropPosition(
                originalImage.Width, 
                originalImage.Height,
                currentRsi.Size.X, 
                currentRsi.Size.Y, 
                shift + rotationFrameShift + frame);
        
            var framePixels = new SKColor[frameHeight, frameWidth];
            for (var fy = 0; fy < frameHeight; fy++)
            {
                for (var fx = 0; fx < frameWidth; fx++)
                {
                    framePixels[fy, fx] = originalImage.GetPixel(x + fx, y + fy);
                }
            }

            encoder.AddFrame(framePixels, delayMs);
        }

        using var stream = new MemoryStream();
        encoder.WriteToStream(stream);
        return stream.ToArray();
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
                $"State index must be between 0 and {totalStates - 1}. Got {stateIndex}");

        var x = (stateIndex % cols) * stateWidth;
        var y = (stateIndex / cols) * stateHeight;

        return (x, y);
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

        LoadingErrorMessage = LocalizationService.GetString("rsi-parse-meta-not-found");
        return null;
    }

    public override async void Execute(CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenFile(cancellationToken);
        LoadFromStream(stream);
    }
}
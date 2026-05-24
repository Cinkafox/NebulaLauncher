using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Nebula.Launcher.Models;

public sealed record RsiJsonMetadata(
[UsedImplicitly]
    Vector2i Size,
    StateJsonMetadata[] States);

[UsedImplicitly]
public sealed record StateJsonMetadata(
    string Name, 
    int? Directions, 
    float[][]? Delays);

[UsedImplicitly]
public struct Vector2i
{
    [JsonInclude] public int X;
    [JsonInclude] public int Y;
    
    override public string ToString() => $"({X}, {Y})";
}
using System;
using System.Text.Json.Serialization;
using Nebula.Launcher.ServerListProviders;

namespace Nebula.Launcher.Models;

public record ListItemTemplate(Type ModelType, string IconKey, string Label);
public record ServerListTabTemplate(IServerListProvider ServerListProvider, string TabName);
public record ServerHubRecord(
    [property:JsonPropertyName("name")] string Name,
    [property:JsonPropertyName("url")] string MainUrl);
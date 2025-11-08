using System.Text.Json.Serialization;
using System.Windows.Input;
using Avalonia.Media;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Models.Auth;

public sealed record ProfileEntry(
    ProfileAuthCredentials Credentials, 
    string AuthName,
    [property: JsonIgnore] ICommand OnSelect = default!,
    [property: JsonIgnore] ICommand OnDelete = default!);
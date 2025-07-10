using System.Text.Json.Serialization;
using System.Windows.Input;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Models.Auth;

public sealed record ProfileAuthCredentials(
    AuthTokenCredentials Credentials,
    [property: JsonIgnore] ICommand OnSelect = default!,
    [property: JsonIgnore] ICommand OnDelete = default!);
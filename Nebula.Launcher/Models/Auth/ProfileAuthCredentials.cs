using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Nebula.Launcher.Models.Auth;

public sealed record ProfileAuthCredentials(
    string Login,
    string Password,
    string AuthServer,
    [property: JsonIgnore] ICommand OnSelect = default!,
    [property: JsonIgnore] ICommand OnDelete = default!);
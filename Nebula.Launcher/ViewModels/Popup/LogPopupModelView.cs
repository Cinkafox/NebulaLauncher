using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(LogPopupView), false)]
[ConstructGenerator]
public sealed partial class LogPopupModelView : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    public override string Title => "LOG";
    public override bool IsClosable => true;

    public ObservableCollection<LogInfo> Logs { get; } = new();

    protected override void InitialiseInDesignMode()
    {
        Logs.Add(new LogInfo
        {
            Category = "DEBG", Message = "MEOW MEOW TEST"
        });

        Logs.Add(new LogInfo
        {
            Category = "ERRO", Message = "MEOW MEOW TEST 11\naaaaa"
        });
    }

    protected override void Initialise()
    {
    }

    public void Append(string str)
    {
        Logs.Add(LogInfo.FromString(str));
    }

    public void Clear()
    {
        Logs.Clear();
    }
}

public sealed class LogInfo
{
    public string Category { get; set; } = "LOG";
    public IBrush CategoryColor { get; set; } = Brush.Parse("#424242");
    public string Message { get; set; } = "";

    public static LogInfo FromString(string input)
    {
        var matches = Regex.Matches(input, @"(\[(?<c>.*)\] (?<m>.*))|(?<m>.*)");
        var category = "All";

        if (matches[0].Groups.TryGetValue("c", out var c)) category = c.Value;

        var color = Brush.Parse("#444444");

        switch (category)
        {
            case "DEBG":
                color = Brush.Parse("#2436d4");
                break;
            case "ERRO":
                color = Brush.Parse("#d42436");
                break;
            case "INFO":
                color = Brush.Parse("#0ab3c9");
                break;
        }

        var message = matches[0].Groups["m"].Value;
        return new LogInfo
        {
            Category = category, Message = message, CategoryColor = color
        };
    }
}
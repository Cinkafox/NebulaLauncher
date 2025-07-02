using System.Collections.ObjectModel;
using Nebula.Launcher.Models;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

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
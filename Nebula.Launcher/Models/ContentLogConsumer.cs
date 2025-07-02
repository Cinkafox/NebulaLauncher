using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Models;

public sealed class ContentLogConsumer : IProcessLogConsumer
{
    private readonly LogPopupModelView _currLog;
    private readonly PopupMessageService _popupMessageService;

    public ContentLogConsumer(LogPopupModelView currLog, PopupMessageService popupMessageService)
    {
        _currLog = currLog;
        _popupMessageService = popupMessageService;
    }

    public void Out(string text)
    {
        _currLog.Append(text);
    }

    public void Error(string text)
    {
        _currLog.Append(text);
    }

    public void Fatal(string text)
    {
        _popupMessageService.Popup("Fatal error while stop instance:" + text);
    }
}
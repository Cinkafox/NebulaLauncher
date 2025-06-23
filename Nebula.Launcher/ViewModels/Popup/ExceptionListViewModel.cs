using System;
using System.Collections.ObjectModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(ExceptionListView), false)]
[ConstructGenerator]
public sealed partial class ExceptionListViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    public override string Title => LocalisationService.GetString("popup-exception");
    public override bool IsClosable => true;

    public ObservableCollection<Exception> Errors { get; } = new();

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
        var e = new Exception("TEST");
        AppendError(e);
    }

    public void AppendError(Exception exception)
    {
        Errors.Add(exception);
        if (exception.InnerException != null)
            AppendError(exception.InnerException);
    }
}
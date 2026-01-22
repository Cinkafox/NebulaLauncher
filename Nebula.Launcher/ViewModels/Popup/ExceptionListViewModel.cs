using System;
using System.Collections.ObjectModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(ExceptionListView), false)]
[ConstructGenerator]
public sealed partial class ExceptionListViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    public override string Title => LocalizationService.GetString("popup-exception");
    public override bool IsClosable => true;

    public ObservableCollection<ExceptionCompound> Errors { get; } = new();

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
        var e = new ExceptionCompound("TEST", "thrown in design mode");
        AppendError(e);
    }

    public void AppendError(ExceptionCompound exception)
    {
        Errors.Add(exception);
    }

    public void AppendError(Exception exception)
    {
        AppendError(new ExceptionCompound(exception));
        if (exception.InnerException != null)
            AppendError(exception.InnerException);
    }
}
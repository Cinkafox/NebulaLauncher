using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(MainView))]
[ConstructGenerator]
public partial class MainViewModel : ViewModelBase
{
    private readonly List<ListItemTemplate> _templates =
    [
        new ListItemTemplate(typeof(AccountInfoViewModel), "user", "Account", null),
        new ListItemTemplate(typeof(ServerListViewModel), "file", "Servers", false),
        new ListItemTemplate(typeof(ServerListViewModel), "star", "Favorites", true),
        new ListItemTemplate(typeof(ContentBrowserViewModel), "folder", "Content", null)
    ];

    private readonly List<PopupViewModelBase> _viewQueue = new();

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private PopupViewModelBase? _currentPopup;
    [ObservableProperty] private string _currentTitle = "Default";
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool _isPaneOpen;
    [ObservableProperty] private bool _isPopupClosable = true;
    [ObservableProperty] private bool _popup;
    [ObservableProperty] private ListItemTemplate? _selectedListItem;

    [GenerateProperty] private DebugService DebugService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private FileService FileService { get; } = default!;

    public ObservableCollection<ListItemTemplate> Items { get; private set; }

    protected override void InitialiseInDesignMode()
    {
        CurrentPage = ViewHelperService.GetViewModel<AccountInfoViewModel>();
        Items = new ObservableCollection<ListItemTemplate>(_templates);
        SelectedListItem = Items.First(vm => vm.ModelType == typeof(AccountInfoViewModel));
    }

    protected override void Initialise()
    {
        InitialiseInDesignMode();

        PopupMessageService.OnPopupRequired += OnPopupRequired;
        PopupMessageService.OnCloseRequired += OnPopupCloseRequired;
        
        CheckMigration();
    }

    private void CheckMigration()
    {
        var loadingHandler = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loadingHandler.LoadingName = "Migration task, please wait...";
        loadingHandler.IsCancellable = false;

        if (!FileService.CheckMigration(loadingHandler))
            return;
        
        OnPopupRequired(loadingHandler);
    }

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        if (!ViewHelperService.TryGetViewModel(value.ModelType, out var vmb) || vmb is not IViewModelPage viewModelPage) return;

        viewModelPage.OnPageOpen(value.args);
        CurrentPage = vmb;
    }

    public void PopupMessage(PopupViewModelBase viewModelBase)
    {
        if (CurrentPopup == null)
        {
            CurrentPopup = viewModelBase;
            CurrentTitle = viewModelBase.Title;
            IsPopupClosable = viewModelBase.IsClosable;
            OnOpenRequired();
        }
        else
        {
            _viewQueue.Add(viewModelBase);
        }
    }

    private void OnCloseRequired()
    {
        IsEnabled = true;
        Popup = false;
    }

    private void OnOpenRequired()
    {
        IsEnabled = false;
        Popup = true;
    }

    public void OpenLink()
    {
        Helper.OpenBrowser("https://cinka.ru/nebula-launcher/");
    }

    private void OnPopupRequired(object viewModelBase)
    {
        switch (viewModelBase)
        {
            case string str:
            {
                var view = ViewHelperService.GetViewModel<InfoPopupViewModel>();
                view.InfoText = str;
                PopupMessage(view);
                break;
            }
            case PopupViewModelBase @base:
                PopupMessage(@base);
                break;
            case Exception error:
                var err = ViewHelperService.GetViewModel<ExceptionListViewModel>();
                DebugService.Error(error);
                err.AppendError(error);
                PopupMessage(err);
                break;
        }
    }

    private void OnPopupCloseRequired(object obj)
    {
        if (obj is not PopupViewModelBase viewModelBase) return;

        if (obj == CurrentPopup)
            ClosePopup();
        else
            _viewQueue.Remove(viewModelBase);
    }


    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    [RelayCommand]
    public void ClosePopup()
    {
        var viewModelBase = _viewQueue.FirstOrDefault();
        if (viewModelBase is null)
        {
            OnCloseRequired();
        }
        else
        {
            CurrentTitle = viewModelBase.Title;
            _viewQueue.RemoveAt(0);
        }

        CurrentPopup = viewModelBase;
    }
}
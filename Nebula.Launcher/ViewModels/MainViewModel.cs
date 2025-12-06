using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(MainView))]
[ConstructGenerator]
public partial class MainViewModel : ViewModelBase
{
    private readonly List<ListItemTemplate> _templates =
    [
        new ListItemTemplate(typeof(AccountInfoViewModel), "user", "tab-account"),
        new ListItemTemplate(typeof(ServerOverviewModel), "file", "tab-servers"),
        new ListItemTemplate(typeof(ContentBrowserViewModel), "folder", "tab-content"),
        new ListItemTemplate(typeof(ConfigurationViewModel), "settings", "tab-settings")
    ];

    private readonly List<PopupViewModelBase> _viewQueue = new();

    [ObservableProperty] private string _versionInfo = "dev";
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private PopupViewModelBase? _currentPopup;
    [ObservableProperty] private string _currentTitle = "Default";
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool _isPaneOpen;
    [ObservableProperty] private bool _isPopupClosable = true;
    [ObservableProperty] private bool _popup;
    [ObservableProperty] private ListItemTemplate? _selectedListItem;
    [ObservableProperty] private string? _loginText = LocalizationService.GetString("auth-current-login-no-name");
    
    [GenerateProperty] private LocalizationService LocalizationService { get; } // Не убирать! Без этой хуйни вся локализация идет в пизду!
    [GenerateProperty] private AccountInfoViewModel AccountInfoViewModel { get; }
    [GenerateProperty] private DebugService DebugService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty] private ContentService ContentService { get; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;

    private ILogger _logger;

    public ObservableCollection<ListItemTemplate> Items { get; private set; }

    protected override void InitialiseInDesignMode()
    {
        Items = new ObservableCollection<ListItemTemplate>(_templates.Select(a=>
        {
            return a with { Label = LocalizationService.GetString(a.Label) };
        }
        ));
        RequirePage<AccountInfoViewModel>();
    }

    protected override void Initialise()
    {
        AccountInfoViewModel.Credentials.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not nameof(AccountInfoViewModel.Credentials.Value)) 
                return;
            UpdateCredentialsInfo();
        };

        UpdateCredentialsInfo();

        _logger = DebugService.GetLogger(this);

        using var stream = typeof(MainViewModel).Assembly
                .GetManifestResourceStream("Nebula.Launcher.Version.txt")!;
        using var streamReader = new StreamReader(stream);

        VersionInfo = streamReader.ReadLine() ?? "dev";

        InitialiseInDesignMode();

        PopupMessageService.OnPopupRequired += OnPopupRequired;
        PopupMessageService.OnCloseRequired += OnPopupCloseRequired;
        
        CheckMigration();
        
        var loadingHandler = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loadingHandler.LoadingName = LocalizationService.GetString("migration-config-task");
        loadingHandler.IsCancellable = false;
        ConfigurationService.MigrateConfigs(loadingHandler);
        
        if (!VCRuntimeDllChecker.AreVCRuntimeDllsPresent())
        {
            OnPopupRequired(LocalizationService.GetString("vcruntime-check-error"));
            Helper.OpenBrowser("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170");
        }
    }

    private void UpdateCredentialsInfo()
    {
        if(AccountInfoViewModel.Credentials.HasValue)
        {
            LoginText =
                LocalizationService.GetString("auth-current-login-name",
                    new Dictionary<string, object>
                    {
                        { "login", AccountInfoViewModel.Credentials.Value?.Login ?? "" },
                        {
                            "auth_server",
                            AccountInfoViewModel.GetServerAuthName(AccountInfoViewModel.Credentials.Value?.AuthServer) ?? ""
                        }
                    });
        }
        else
        {
            LoginText =  LocalizationService.GetString("auth-current-login-no-name");
        }
    }

    private void CheckMigration()
    {
        if (!ConfigurationService.GetConfigValue(LauncherConVar.DoMigration))
            return;

        var loadingHandler = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loadingHandler.LoadingName = LocalizationService.GetString("migration-label-task");
        loadingHandler.IsCancellable = false;

        if (!ContentService.CheckMigration(loadingHandler))
            return;

        OnPopupRequired(loadingHandler);
        ConfigurationService.SetConfigValue(LauncherConVar.DoMigration, false);
    }

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        if (!ViewHelperService.TryGetViewModel(value.ModelType, out var vmb)) return;

        OpenPage(vmb, false);
    }

    public T RequirePage<T>() where T : ViewModelBase
    {
        if (CurrentPage is T vam) return vam;
        
        var page = ViewHelperService.GetViewModel<T>();
        OpenPage(page);
        return page;
    }

    private void OpenPage(ViewModelBase obj, bool selectListView = true) 
    {
        var tabItems = Items.Where(vm => vm.ModelType == obj.GetType());

        if(selectListView)
        {
            var listItemTemplates = tabItems as ListItemTemplate[] ?? tabItems.ToArray();
            if (listItemTemplates.Length != 0)
            {
                SelectedListItem = listItemTemplates.First();
            }
        }
        
        CurrentPage = obj;
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

    public void OpenAuthPage()
    {
        RequirePage<AccountInfoViewModel>();
    }

    public void OpenRootPath()
    {
        ExplorerHelper.OpenFolder(FileService.RootPath);
    }

    public void OpenLink()
    {
        Helper.OpenBrowser("https://durenko.tatar/nebula");
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
                _logger.Error(error);
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

public static class VCRuntimeDllChecker
{
    public static bool AreVCRuntimeDllsPresent()
    {
        if (!OperatingSystem.IsWindows()) return true;
        
        string systemDir = Environment.SystemDirectory;
        string[] requiredDlls = {
            "msvcp140.dll",
            "vcruntime140.dll"
        };

        foreach (var dll in requiredDlls)
        {
            var path = Path.Combine(systemDir, dll);
            if (!File.Exists(path))
            {
                return false;
            }
        }

        return true;
    }
}
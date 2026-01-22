using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nebula.Launcher.Models;
using Nebula.Launcher.ViewModels;

namespace Nebula.Launcher.Views;

public partial class ExceptionView : UserControl
{
    public ExceptionView()
    {
        InitializeComponent();
    }

    public ExceptionView(Exception exception): this()
    {
        DataContext = new ExceptionCompound(exception);
    }
}
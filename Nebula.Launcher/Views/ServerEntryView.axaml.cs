using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Nebula.Launcher.ViewModels;

namespace Nebula.Launcher.Views;

public partial class ServerEntryView : UserControl
{
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(25);
    private readonly DispatcherTimer _scrollTimer;
    private TimeSpan _currTime = TimeSpan.Zero;
    
    public ServerEntryView()
    {
        _scrollTimer = new DispatcherTimer
        {
            Interval = _interval
        }; 
    
        InitializeComponent();
        StartAutoScrolling();
    }
    

    
    private void StartAutoScrolling()
    {
        _scrollTimer.Tick += AutoScroll;
        _scrollTimer.Start();
    }

    private void AutoScroll(object? sender, EventArgs e)
    {
        var maxOffset = AutoScrollViewer.Extent.Width - AutoScrollViewer.Viewport.Width;
        var value = (Math.Sin(_currTime.TotalSeconds / 2) + 1) * (maxOffset / 2) ;
        
        AutoScrollViewer.Offset = new Avalonia.Vector(value, AutoScrollViewer.Offset.Y);
        _currTime += _interval;
        if (_currTime > TimeSpan.FromSeconds(10000)) _currTime = TimeSpan.Zero;
    }
}
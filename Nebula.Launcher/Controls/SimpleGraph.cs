using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;

namespace Nebula.Launcher.Controls;

public class SimpleGraph : Control
{
    // Bindable data: list of doubles or points
    public static readonly StyledProperty<ObservableCollection<double>> ValuesProperty =
        AvaloniaProperty.Register<SimpleGraph,ObservableCollection<double>>(nameof(Values));

    public static readonly StyledProperty<IBrush> GraphBrushProperty =
        AvaloniaProperty.Register<SimpleGraph, IBrush>(nameof(GraphBrush), Brushes.CornflowerBlue);
    
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<SimpleGraph, IBrush>(nameof(GridBrush), Brushes.LightGray);

    static SimpleGraph()
    {
        ValuesProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ObservableCollection<double>>>(args =>
            {
                if (args.Sender is not SimpleGraph g) 
                    return;
                
                g.InvalidateVisual();
                g.Values.CollectionChanged += g.ValuesOnCollectionChanged;
            }));
    }
    
    public SimpleGraph()
    {
        Values = new ObservableCollection<double>();
    }

    private void ValuesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    public ObservableCollection<double> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }


    public IBrush GraphBrush
    {
        get => GetValue(GraphBrushProperty);
        set => SetValue(GraphBrushProperty, value);
    }
    
    public IBrush GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        
        // background grid
        DrawGrid(context, Bounds);
        
        if (Values.Count == 0)
            return;


        var min = Values.Min();
        var max = Values.Max();
        if (Math.Abs(min - max) < 0.001)
        {
            min -= 1;
            max += 1;
        }


        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            if (Values.Count > 1)
            {
                Point p0 = Map(0, Values[0]);
                ctx.BeginFigure(p0, false);


                for (int i = 0; i < Values.Count - 1; i++)
                {
                    var p1 = Map(i, Values[i]);
                    var p2 = Map(i + 1, Values[i + 1]);


                    // control points for smoothing
                    var c1 = new Point((p1.X + p2.X) / 2, p1.Y);
                    var c2 = new Point((p1.X + p2.X) / 2, p2.Y);


                    ctx.CubicBezierTo(c1, c2, p2);
                }
                ctx.EndFigure(false);
            }
        }


        // stroke
        context.DrawGeometry(null, new Pen(GraphBrush, 2), geo);
        
        // draw points
        for (var i = 0; i < Values.Count; i++)
        {
            var p = Map(i, Values[i]);
            context.DrawEllipse(GraphBrush, null, p, 3, 3);
        }

        return;

        // map data index/value -> point
        Point Map(int i, double val)
        {
            var x = Bounds.X + Bounds.Width * (i / (double)Math.Max(1, Values.Count - 1));
            var y = Bounds.Y + Bounds.Height - (val - min) / (max - min) * Bounds.Height;
            return new Point(x, y);
        }
    }

    private void DrawGrid(DrawingContext dc, Rect r)
    {
        var pen = new Pen(GridBrush, 0.5);
        var rows = 4;
        var cols = Math.Max(2, Values?.Count ?? 2);
        for (var i = 0; i <= rows; i++)
        {
            var y = r.Y + i * (r.Height / rows);
            dc.DrawLine(pen, new Point(r.X, y), new Point(r.Right, y));
        }

        for (var j = 0; j <= cols; j++)
        {
            var x = r.X + j * (r.Width / cols);
            dc.DrawLine(pen, new Point(x, r.Y), new Point(x, r.Bottom));
        }
    }
}
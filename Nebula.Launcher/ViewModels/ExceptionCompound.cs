using System;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ExceptionView), false)]
public class ExceptionCompound : ViewModelBase
{
    public ExceptionCompound()
    {
        Message = "Test exception";
        StackTrace = "Stack trace";
    }
    
    public ExceptionCompound(string message, string stackTrace)
    {
        Message = message;
        StackTrace = stackTrace;
    }

    public ExceptionCompound(Exception ex)
    {
        Message = ex.Message;
        StackTrace = ex.StackTrace;
    }

    public string Message { get; set; }
    public string? StackTrace { get; set; }
    
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}
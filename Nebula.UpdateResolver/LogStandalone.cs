using System;

namespace Nebula.UpdateResolver;

public static class LogStandalone
{
    public static Action<string, int>? OnLog;
    
    public static void LogError(Exception e){
        Log($"{e.GetType().Name}: "+ e.Message);
        Log(e.StackTrace);
        if(e.InnerException != null) 
            LogError(e.InnerException);
    }
    
    public static void Log(string? message, int percentage = 0)
    {
        if(message is null) return;
        
        OnLog?.Invoke(message, percentage);
    }
}
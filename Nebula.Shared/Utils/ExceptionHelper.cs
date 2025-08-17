namespace Nebula.Shared.Utils;

public static class ExceptionHelper
{
    public static Task<T> TryRun<T>(Func<Task<T>> func, int attempts = 3, Action<int, Exception>? attemptsCallback = null)
    {
        try
        {
            return func.Invoke();
        }
        catch (Exception e)
        {
            if (attempts <= 0) throw new("Attempts was expired! ", e);
            attempts--;
            attemptsCallback?.Invoke(attempts, e);
            return TryRun(func, attempts);
        }
    }
}
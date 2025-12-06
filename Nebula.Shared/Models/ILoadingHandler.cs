namespace Nebula.Shared.Models;

public interface ILoadingHandler : IDisposable
{
    public void SetJobsCount(long count);
    public long GetJobsCount();

    public void SetResolvedJobsCount(long count);
    public long GetResolvedJobsCount();
    public void SetLoadingMessage(string message);

    public void AppendJob(long count = 1)
    {
        SetJobsCount(GetJobsCount() + count);
    }

    public void AppendResolvedJob(long count = 1)
    {
        SetResolvedJobsCount(GetResolvedJobsCount() + count);
    }

    public void Clear()
    {
        SetResolvedJobsCount(0);
        SetJobsCount(0);
    }

    public QueryJob GetQueryJob()
    {
        return new QueryJob(this);
    }
}

public interface ILoadingFormater
{
    public string Format(ILoadingHandler loadingHandler);
}

public interface ILoadingHandlerFactory: IDisposable
{
    public ILoadingHandler CreateLoadingContext(ILoadingFormater? loadingFormater = null);
}

public interface IConnectionSpeedHandler
{
    public void PasteSpeed(int speed);
}

public sealed class DefaultLoadingFormater : ILoadingFormater
{
    public static DefaultLoadingFormater Instance = new DefaultLoadingFormater();
    public string Format(ILoadingHandler loadingHandler)
    {
        return loadingHandler.GetResolvedJobsCount() + "/" + loadingHandler.GetJobsCount();
    }
}

public sealed class FileLoadingFormater : ILoadingFormater
{
    public string Format(ILoadingHandler loadingHandler)
    {
        return FormatBytes(loadingHandler.GetResolvedJobsCount()) + " / " + FormatBytes(loadingHandler.GetJobsCount());
    }
    
    public static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;
        const long TB = GB * 1024;

        if (bytes >= TB)
            return $"{bytes / (double)TB:0.##} TB";
        if (bytes >= GB)
            return $"{bytes / (double)GB:0.##} GB";
        if (bytes >= MB)
            return $"{bytes / (double)MB:0.##} MB";
        if (bytes >= KB)
            return $"{bytes / (double)KB:0.##} KB";

        return $"{bytes} B";
    }
}

public sealed class QueryJob : IDisposable
{
    private readonly ILoadingHandler _handler;

    public QueryJob(ILoadingHandler handler)
    {
        _handler = handler;
        handler.AppendJob();
    }

    public void Dispose()
    {
        _handler.AppendResolvedJob();
    }
}
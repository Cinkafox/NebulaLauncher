namespace Nebula.Shared.Models;

public interface ILoadingHandler
{
    public void SetJobsCount(int count);
    public int GetJobsCount();

    public void SetResolvedJobsCount(int count);
    public int GetResolvedJobsCount();
    public void SetLoadingMessage(string message);

    public void AppendJob(int count = 1)
    {
        SetJobsCount(GetJobsCount() + count);
    }

    public void AppendResolvedJob(int count = 1)
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
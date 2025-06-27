using Nebula.Runner.Services;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Robust.LoaderApi;

namespace Nebula.Runner;

[ServiceRegister]
public sealed class App(RunnerService runnerService, ContentService contentService)
    : IRedialApi
{
    public void Redial(Uri uri, string text = "")
    {
    }

    public async Task Run(string[] args1)
    {
        var login = Environment.GetEnvironmentVariable("AUTH_LOGIN") ?? "Alexandra";
        var urlraw = Environment.GetEnvironmentVariable("GAME_URL") ?? "ss14://localhost";

        var url = urlraw.ToRobustUrl();

        using var cancelTokenSource = new CancellationTokenSource();
        var buildInfo = await contentService.GetBuildInfo(url, cancelTokenSource.Token);


        var args = new List<string>
        {
            // Pass username to launched client.
            // We don't load username from client_config.toml when launched via launcher.
            "--username", login,

            // Tell game we are launcher
            "--cvar", "launch.launcher=true"
        };

        var connectionString = url.ToString();
        if (!string.IsNullOrEmpty(buildInfo.BuildInfo.ConnectAddress))
            connectionString = buildInfo.BuildInfo.ConnectAddress;

        // We are using the launcher. Don't show main menu etc..
        // Note: --launcher also implied --connect.
        // For this reason, content bundles do not set --launcher.
        args.Add("--launcher");

        args.Add("--connect-address");
        args.Add(connectionString);

        args.Add("--ss14-address");
        args.Add(url.ToString());

        await runnerService.Run(args.ToArray(), buildInfo, this, new ConsoleLoadingHandler(), cancelTokenSource.Token);
    }
}

public sealed class ConsoleLoadingHandler : ILoadingHandler
{
    private int _currJobs;

    private float _percent;
    private int _resolvedJobs;

    public void SetJobsCount(int count)
    {
        _currJobs = count;

        UpdatePercent();
        Draw();
    }

    public int GetJobsCount()
    {
        return _currJobs;
    }

    public void SetResolvedJobsCount(int count)
    {
        _resolvedJobs = count;

        UpdatePercent();
        Draw();
    }

    public int GetResolvedJobsCount()
    {
        return _resolvedJobs;
    }

    private void UpdatePercent()
    {
        if (_currJobs == 0)
        {
            _percent = 0;
            return;
        }

        if (_resolvedJobs > _currJobs) return;

        _percent = _resolvedJobs / (float)_currJobs;
    }

    private void Draw()
    {
        var barCount = 10;
        var fullCount = (int)(barCount * _percent);
        var emptyCount = barCount - fullCount;

        Console.Write("\r");

        for (var i = 0; i < fullCount; i++) Console.Write("#");

        for (var i = 0; i < emptyCount; i++) Console.Write(" ");

        Console.Write($"\t {_resolvedJobs}/{_currJobs}");
    }
}
using Nebula.Runner.Services;
using Nebula.Shared;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Robust.LoaderApi;

namespace Nebula.Runner;

[ServiceRegister]
public sealed class App(RunnerService runnerService, ContentService contentService, DebugService debugService, RedialService redialService): IRedialApi
{
    public ILogger logger = debugService.GetLogger("Runner");
    
    private readonly CancellationTokenSource _cancelTokenSource = new();

    public async Task Run(string[] args1)
    {
        var login = Environment.GetEnvironmentVariable("AUTH_LOGIN") ?? "Alexandra";
        var urlraw = Environment.GetEnvironmentVariable("GAME_URL") ?? "ss14://localhost";
        var url = urlraw.ToRobustUrl();
        
        try
        {
            var buildInfo = await contentService.GetBuildInfo(url, _cancelTokenSource.Token);


            var args = new List<string>
            {
                "--username", login,
                "--cvar", "launch.launcher=true"
            };

            var connectionString = url.ToString();
            if (!string.IsNullOrEmpty(buildInfo.BuildInfo.ConnectAddress))
                connectionString = buildInfo.BuildInfo.ConnectAddress;
        
            args.Add("--launcher");

            args.Add("--connect-address");
            args.Add(connectionString);

            args.Add("--ss14-address");
            args.Add(url.ToString());

            await runnerService.Run(args.ToArray(), buildInfo, this, new ConsoleLoadingHandlerFactory(), login, _cancelTokenSource.Token);
        }
        catch (Exception e)
        {
            logger.Error(e);
            throw;
        }
    }

    public void Redial(Uri uri, string text = "")
    {
        _cancelTokenSource.Cancel();
        redialService.Redial(uri, text);
    }
}

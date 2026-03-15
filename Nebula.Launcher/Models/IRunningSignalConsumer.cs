namespace Nebula.Launcher.Models;

public interface IRunningSignalConsumer
{
    public void ProcessRunningSignal(bool isRunning);
}
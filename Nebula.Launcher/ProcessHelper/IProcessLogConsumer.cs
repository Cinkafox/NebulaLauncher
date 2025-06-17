namespace Nebula.Launcher.ProcessHelper;

public interface IProcessLogConsumer
{
    public void Out(string text);
    public void Error(string text);
    public void Fatal(string text);
}
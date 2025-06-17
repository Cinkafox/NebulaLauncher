namespace Nebula.Launcher.ProcessHelper;

public interface IProcessConsumerCollection
{
    public void RegisterLogger(IProcessLogConsumer consumer);
}
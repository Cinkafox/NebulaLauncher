using System.Collections.Generic;

namespace Nebula.Launcher.ProcessHelper;

public sealed class ProcessLogConsumerCollection: IProcessLogConsumer, IProcessConsumerCollection
{
    private readonly List<IProcessLogConsumer> _consumers = [];

    public void RegisterLogger(IProcessLogConsumer consumer)
    {
        _consumers.Add(consumer);
    }

    public void Out(string text)
    {
        foreach (var consumer in _consumers)
        {
            consumer.Out(text);
        }
    }

    public void Error(string text)
    {
        foreach (var consumer in _consumers)
        {
            consumer.Error(text);
        }
    }

    public void Fatal(string text)
    {
        foreach (var consumer in _consumers)
        {
            consumer.Fatal(text);
        }
    }
}
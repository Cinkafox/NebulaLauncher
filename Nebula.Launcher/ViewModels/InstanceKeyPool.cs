namespace Nebula.Launcher.ViewModels;

public sealed class InstanceKeyPool
{
    private int _nextId = 1;

    public InstanceKey Take()
    {
        return new InstanceKey(_nextId++);
    }

    public void Free(InstanceKey id)
    {
        // TODO: make some free logic later
    }
}
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.Models;

public interface IFilterConsumer
{
    public void ProcessFilter(ServerFilter? serverFilter);
}
namespace Nebula.Launcher.ViewModels.Pages;

public interface IConfigControl
{
    public string ConfigName { get; }
    public bool Dirty {get;}
    public abstract void SetValue(object value);
    public abstract object GetValue();
}
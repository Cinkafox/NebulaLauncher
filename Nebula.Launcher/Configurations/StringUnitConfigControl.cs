namespace Nebula.Launcher.ViewModels.Pages;

public sealed class StringUnitConfigControl(string name, string value) : UnitConfigControl<string>(name, value)
{
    public override void SetConfValue(string value)
    {
        ConfigValue = value;
    }

    public override string GetConfValue()
    {
        return ConfigValue;
    }
}
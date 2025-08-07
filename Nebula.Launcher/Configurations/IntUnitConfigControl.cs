namespace Nebula.Launcher.ViewModels.Pages;

public sealed class IntUnitConfigControl(string name, int value) : UnitConfigControl<int>(name, value)
{
    public override void SetConfValue(int value)
    {
        ConfigValue = value.ToString();
    }

    public override int GetConfValue()
    {
        return int.Parse(ConfigValue);
    }
}
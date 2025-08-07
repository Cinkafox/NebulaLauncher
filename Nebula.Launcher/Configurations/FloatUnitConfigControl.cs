using System.Globalization;

namespace Nebula.Launcher.ViewModels.Pages;

public sealed class FloatUnitConfigControl(string name, float value) : UnitConfigControl<float>(name, value)
{

    public CultureInfo CultureInfo = CultureInfo.InvariantCulture;

    public override void SetConfValue(float value)
    {
        ConfigValue = value.ToString(CultureInfo);
    }

    public override float GetConfValue()
    {
        return float.Parse(ConfigValue, CultureInfo);
    }
}
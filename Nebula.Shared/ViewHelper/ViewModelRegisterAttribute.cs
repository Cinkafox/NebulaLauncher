namespace Nebula.Shared.ViewHelper;

[AttributeUsage(AttributeTargets.Class)]
public class ViewModelRegisterAttribute(Type? type = null, bool isSingleton = true) : Attribute
{
    public Type? Type { get; } = type;
    public bool IsSingleton { get; } = isSingleton;
}
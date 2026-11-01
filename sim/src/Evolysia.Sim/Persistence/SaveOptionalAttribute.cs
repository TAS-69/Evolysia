namespace Evolysia.Sim.Persistence;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SaveOptionalAttribute(int integerDefault = 0) : Attribute
{
    public int IntegerDefault { get; } = integerDefault;
}

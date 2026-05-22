namespace Orbitality.ComponentUtils.Modifiers;

public abstract class DataModifier<T>
{
    public DataProperty<T> Property { get; init; }
}
namespace Orbitality.Components;

public record struct EntityMetaData(string Name, string Tag, ushort Icon)
{
    public static EntityMetaData CreateDefault()
    {
        return new EntityMetaData("NewEntity", "Default", 0);
    }
}
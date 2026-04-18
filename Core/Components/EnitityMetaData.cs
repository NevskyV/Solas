namespace Core.Components;

public record struct EntityMetaData(string Name, string Tag, ushort Icon)
{
    public static EntityMetaData CreateDefault() =>
        new("NewEntity", "Default", 0);
}

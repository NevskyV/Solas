namespace Core.Components;

public struct EntityMetaData (string name, string tag, ushort icon)
{
    public string Name { get; set; } = name;
    public string Tag { get; set; } = tag;
    public ushort Icon { get; set; } = icon;
}
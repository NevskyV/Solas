using Solas.Attributes;
using Solas.Components;

namespace Solas.Render;

[SettingsSection]
public class WindowSettings : IData
{
    public Entity Entity { get; set; }

    public short Width = 1200;
    public short Height = 1000;
    public string WindowTitle = "Solas Game";
    public bool Vsync;
    public ushort Api;
    public ushort StartWindowsState;
    public string IconPath = "";
}
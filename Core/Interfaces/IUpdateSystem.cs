using Solas.Enums;

namespace Solas.Interfaces;

public interface IUpdateSystem
{
    public UpdateType UpdateType { get; }
    public void Update();
}
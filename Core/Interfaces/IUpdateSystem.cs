using Solas.Enums;
using Solas.World;

namespace Solas.Interfaces;

public interface IUpdateSystem
{
    public UpdateType UpdateType { get; }
    public void Update(Space space);
}
using Orbitality.Enums;

namespace Orbitality.Interfaces;

public interface IUpdateSystem
{
    public UpdateType UpdateType { get;}
    public void Update();
}
namespace Orbitality.Interfaces;

public enum UpdateType { Update, FixedUpdate, LateUpdate }

public interface IUpdateSystem
{
    public UpdateType UpdateType { get; }
    public void Update();
}
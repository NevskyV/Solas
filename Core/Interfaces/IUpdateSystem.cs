namespace Orbitality.Interfaces;

public interface IUpdateSystem
{
    public UpdateType UpdateType { get;}
    public void Update();
}

public enum UpdateType { Update, FixedUpdate, LateUpdate }
namespace Solas.Interfaces;

public interface IUpdateRunner
{
    public void InjectPools(ReadOnlySpan<IComponentPool> pools);
    public void Run();
}
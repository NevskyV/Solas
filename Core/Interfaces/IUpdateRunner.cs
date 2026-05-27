namespace Orbitality.Interfaces;

public interface IUpdateRunner
{
    void InjectPools(ReadOnlySpan<IComponentPool> pools);
    void Run();
}
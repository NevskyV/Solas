using Core.Interfaces;

namespace Core.Components;

public class UpdateRunner<T>(ComponentPool<T> pool) : IUpdateRunner
    where T : IUpdatable
{
    public void Run()
    {
        var components = pool.Components;

        for (int i = 0; i < components.Count; i++)
            components[i].Update();
    }
}

public class FixedUpdateRunner<T>(ComponentPool<T> pool) : IUpdateRunner
    where T : IFixedUpdatable
{
    public void Run()
    {
        var components = pool.Components;

        for (int i = 0; i < components.Count; i++)
            components[i].FixedUpdate();
    }
}

public class LateUpdateRunner<T>(ComponentPool<T> pool) : IUpdateRunner
    where T : ILateUpdatable
{
    public void Run()
    {
        var components = pool.Components;

        for (int i = 0; i < components.Count; i++)
            components[i].LateUpdate();
    }
}
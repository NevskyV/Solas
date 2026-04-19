using System.Diagnostics;
using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Systems;

public static class Time
{
    public static double DeltaTime { get; internal set; }
    public static double FixedDeltaTime { get; set; } = 0.02;
    public static double TimeScale { get; set; } = 1.0;

    public static double Alpha { get; internal set; }
}

public class Updater
{
    private readonly List<IUpdatable> _updatables = new();
    private readonly List<IFixedUpdatable> _fixedUpdatables = new();

    private readonly Stopwatch _stopwatch = new();

    private double _previousTime;
    private double _accumulator;

    private const int MaxFixedStepsPerTick = 5;

    public void Start()
    {
        _stopwatch.Restart();
        _previousTime = _stopwatch.Elapsed.TotalSeconds;
        _accumulator = 0;
    }

    public void Tick()
    {
        double currentTime = _stopwatch.Elapsed.TotalSeconds;
        double frameTime = currentTime - _previousTime;
        _previousTime = currentTime;
        
        frameTime = Math.Min(frameTime, 0.25);
        
        double scaledDelta = frameTime * Time.TimeScale;

        Time.DeltaTime = scaledDelta;

        _accumulator += scaledDelta;
        
        int steps = 0;

        while (_accumulator >= Time.FixedDeltaTime && steps < MaxFixedStepsPerTick)
        {
            foreach (var f in _fixedUpdatables)
                f?.FixedUpdate();

            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }
        
        if (steps == MaxFixedStepsPerTick)
            _accumulator = 0;
        
        Time.Alpha = _accumulator / Time.FixedDeltaTime;
        foreach (var u in _updatables)
            u?.Update();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
    
    public void SetupUpdatables(Space space)
    {
        foreach (var entity in space.Entities)
        {
            foreach (var logic in entity.Logics)
            {
                AddUpdatable(logic);
            }
        }
    }

    public void AddUpdatable(object obj)
    {
        if (obj is IUpdatable u) _updatables.Add(u);
        if (obj is IFixedUpdatable f) _fixedUpdatables.Add(f);
        Console.WriteLine($"{obj} has been added");
    }

    public void RemoveUpdatable(object obj)
    {
        if (obj is IUpdatable u) _updatables.Remove(u);
        if (obj is IFixedUpdatable f) _fixedUpdatables.Remove(f);
        Console.WriteLine($"{obj} has been removed");
    }
}
using System.Diagnostics;
using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Systems;

public static class Time
{
    public static double DeltaTime { get; internal set; }
    public static double FixedDeltaTime { get; set; } = 0.02;
    public static double Alpha { get; internal set; }
}

public class Updater
{
    private readonly List<IUpdatable> _updatables = new();
    private readonly List<IFixedUpdatable> _fixedUpdatables = new();

    private bool _running = true;
    
    public double TargetFps { get; set; } = 60.0;
    
    private const int MaxFixedStepsPerFrame = 5;
    
    public void SetupUpdatables(Space space)
    {
        foreach (var entity in space.Entities)
        {
            foreach (var logic in entity.Logics)
            {
                AddUpdatableLogic(logic);
            }
        }
    }

    public void AddUpdatableLogic(Logic logic)
    {
        if(logic is IUpdatable) _updatables.Add(logic as IUpdatable);
        if(logic is IFixedUpdatable) _fixedUpdatables.Add(logic as IFixedUpdatable);
    }

    public void RemoveUpdatableLogic(Logic logic)
    {
        if(logic is IUpdatable) _updatables.Remove(logic as IUpdatable);
        if(logic is IFixedUpdatable) _fixedUpdatables.Remove(logic as IFixedUpdatable);
    }
    
    public async void Run()
    {
        var stopwatch = Stopwatch.StartNew();

        double previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0.0;

        double targetFrameTime = 1.0 / TargetFps;

        while (_running)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double frameTime = currentTime - previousTime;
            previousTime = currentTime;
            
            frameTime = Math.Min(frameTime, 0.25);

            Time.DeltaTime = frameTime;
            accumulator += frameTime;
            
            int steps = 0;
            while (accumulator >= Time.FixedDeltaTime && steps < MaxFixedStepsPerFrame)
            {
                foreach (var f in _fixedUpdatables)
                    f.FixedUpdate();

                accumulator -= Time.FixedDeltaTime;
                steps++;
            }
            
            if (steps == MaxFixedStepsPerFrame)
                accumulator = 0;
            
            Time.Alpha = accumulator / Time.FixedDeltaTime;
            
            foreach (var u in _updatables)
                u.Update();
            
            double frameEndTime = stopwatch.Elapsed.TotalSeconds;
            double elapsed = frameEndTime - currentTime;
            double sleepTime = targetFrameTime - elapsed;

            if (sleepTime > 0)
            {
                int ms = (int)(sleepTime * 1000);

                if (ms > 1)
                    Thread.Sleep(ms - 1);
                
                while ((stopwatch.Elapsed.TotalSeconds - currentTime) < targetFrameTime)
                {
                    Thread.SpinWait(10);
                }
            }
        }
    }

    public void Stop() => _running = false;
}
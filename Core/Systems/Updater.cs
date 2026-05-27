using System.Diagnostics;
using System.Runtime.InteropServices;
using Orbitality.Containers;
using Orbitality.Interfaces;

namespace Orbitality.Systems;

public static class Time
{
    public static double DeltaTime { get; internal set; }
    public static double FixedDeltaTime { get; set; } = 0.02;
    public static double TimeScale { get; set; } = 1.0;
    public static double Alpha { get; internal set; }
}

public class Updater
{
    public float TargetFrameTime { get; set; } = 60.0f;
    private readonly Stopwatch _stopwatch = new();

    private double _previousTime;
    private double _accumulator;

    private const int MaxFixedStepsPerTick = 5;
    private readonly double _tickToSeconds = 1.0 / Stopwatch.Frequency;

    private bool _isRunning;
    private EntityPool _entityPool;

    public readonly List<IUpdateSystem> UpdateSystems = [];
    public readonly List<IUpdateSystem> FixedUpdateSystems = [];
    public readonly List<IUpdateSystem> LateUpdateSystems = [];

    public void Start(EntityPool entityPool)
    {
        if (_isRunning) return;
        _entityPool = entityPool;

        _stopwatch.Restart();
        _previousTime = _stopwatch.Elapsed.TotalSeconds;
        _accumulator = 0;
        _isRunning = true;

        var injectAction = Engine.Context.SpacePool.InjectPoolsInUpdateRunners;
        
        var e = Engine.Instance;
        while (e.State != GameState.None)
        {
            double startTicks = _stopwatch.ElapsedTicks;
            
            //Injecting runners before Tick
            injectAction.Invoke(CollectionsMarshal.AsSpan(_entityPool.UpdateRunners));
            injectAction.Invoke(CollectionsMarshal.AsSpan(_entityPool.FixedUpdateRunners));
            injectAction.Invoke(CollectionsMarshal.AsSpan(_entityPool.LateUpdateRunners));
            
            Tick();

            var targetTicks = 1.0 / TargetFrameTime * Stopwatch.Frequency;
            var elapsedTicks = _stopwatch.ElapsedTicks - startTicks;

            var remaining = targetTicks - elapsedTicks;

            if (remaining > 0)
            {
                var ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                if (ms > 0)
                    Thread.Sleep(ms);
                while (e.State != GameState.None && _stopwatch.ElapsedTicks - startTicks < targetTicks)
                    Thread.SpinWait(10);
            }
        }

        _isRunning = false;
    }

    public void Tick()
    {
        var currentTime = _stopwatch.ElapsedTicks * _tickToSeconds;
        var frameTime = currentTime - _previousTime;
        _previousTime = currentTime;

        frameTime = Math.Min(frameTime, 0.25);

        var scaledDelta = frameTime * Time.TimeScale;

        Time.DeltaTime = scaledDelta;

        if (Time.TimeScale > 0) _accumulator += scaledDelta;

        var steps = 0;
        while (_accumulator >= Time.FixedDeltaTime && steps < MaxFixedStepsPerTick)
        {
            for (var i = 0; i < FixedUpdateSystems.Count; i++) FixedUpdateSystems[i].Update();
            var fixedUpdatables = _entityPool.FixedUpdateRunners;
            for (var i = 0; i < fixedUpdatables.Count; i++) fixedUpdatables[i].Run();
            
            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }

        _accumulator = Math.Min(_accumulator, Time.FixedDeltaTime);

        var invFixedDelta = 1.0 / Time.FixedDeltaTime;
        Time.Alpha = _accumulator * invFixedDelta;

        for (var i = 0; i < UpdateSystems.Count; i++) UpdateSystems[i].Update();
        var updatables = _entityPool.UpdateRunners;
        for (var i = 0; i < updatables.Count; i++) updatables[i].Run();
        
        for (var i = 0; i < LateUpdateSystems.Count; i++) LateUpdateSystems[i].Update();
        var lateUpdatables = _entityPool.LateUpdateRunners;
        for (var i = 0; i < lateUpdatables.Count; i++) lateUpdatables[i].Run();
        
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
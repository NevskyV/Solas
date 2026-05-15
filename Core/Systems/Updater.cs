using System.Diagnostics;
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

    public IUpdateSystem[] UpdateSystems;
    public IUpdateSystem[] FixedUpdateSystems;
    public IUpdateSystem[] LateUpdateSystems;

    public void Start(EntityPool entityPool)
    {
        if (_isRunning) return;
        _entityPool = entityPool;

        _stopwatch.Restart();
        _previousTime = _stopwatch.Elapsed.TotalSeconds;
        _accumulator = 0;
        _isRunning = true;
        
        var e = Engine.Instance;
        while (e.State != GameState.None)
        {
            double startTicks = _stopwatch.ElapsedTicks;

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
            var fixedUpdatables = _entityPool.FixedUpdateRunners;
            for (var i = 0; i < fixedUpdatables.Count; i++) fixedUpdatables[i].Run();
            for (var i = 0; i < FixedUpdateSystems.Length; i++) FixedUpdateSystems[i].Update();
            
            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }

        _accumulator = Math.Min(_accumulator, Time.FixedDeltaTime);

        var invFixedDelta = 1.0 / Time.FixedDeltaTime;
        Time.Alpha = _accumulator * invFixedDelta;

        var updatables = _entityPool.UpdateRunners;
        for (var i = 0; i < updatables.Count; i++) updatables[i].Run();
        for (var i = 0; i < UpdateSystems.Length; i++) UpdateSystems[i].Update();
        
        var lateUpdatables = _entityPool.LateUpdateRunners;
        for (var i = 0; i < lateUpdatables.Count; i++) lateUpdatables[i].Run();
        for (var i = 0; i < LateUpdateSystems.Length; i++) LateUpdateSystems[i].Update();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
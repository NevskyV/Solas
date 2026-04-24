using System.Diagnostics;
using Core.Containers;

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
    public float TargetFrameTime { get; set; } =  1.0f / 60.0f;
    private readonly Stopwatch _stopwatch = new();

    private double _previousTime;
    private double _accumulator;

    private const int MaxFixedStepsPerTick = 5;
    private readonly double _tickToSeconds = 1.0 / Stopwatch.Frequency;

    private bool _isRunning;
    private EntityPool _entityPool;
    

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
            long startTicks = _stopwatch.ElapsedTicks;

            Tick();

            long targetTicks = (long)(TargetFrameTime * Stopwatch.Frequency);
            long elapsedTicks = _stopwatch.ElapsedTicks - startTicks;

            long remaining = targetTicks - elapsedTicks;

            if (remaining > 0)
            {
                int ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                if (ms > 0)
                    Thread.Sleep(ms);
                while ((_stopwatch.ElapsedTicks - startTicks) < targetTicks)
                    Thread.SpinWait(10);
            }
        }
        _isRunning = false;
    }

    public void Tick()
    {
        double currentTime = _stopwatch.ElapsedTicks * _tickToSeconds;
        double frameTime = currentTime - _previousTime;
        _previousTime = currentTime;
        
        frameTime = Math.Min(frameTime, 0.25);
        
        double scaledDelta = frameTime * Time.TimeScale;

        Time.DeltaTime = scaledDelta;

        if (Time.TimeScale > 0)
        {
            _accumulator += scaledDelta;
        }
        
        int steps = 0;

        while (_accumulator >= Time.FixedDeltaTime && steps < MaxFixedStepsPerTick)
        {
            var fixedUpdatables = _entityPool.FixedUpdatables;
            for (int i = 0; i < fixedUpdatables.Count; i++)
            {
                fixedUpdatables[i].FixedUpdate();
            }

            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }
        
        _accumulator = Math.Min(_accumulator, Time.FixedDeltaTime);
        
        double invFixedDelta = 1.0 / Time.FixedDeltaTime;
        Time.Alpha = _accumulator * invFixedDelta;
        
        var updatables = _entityPool.Updatables;
        for (int i = 0; i < updatables.Count; i++)
        {
            updatables[i].Update();
        }
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
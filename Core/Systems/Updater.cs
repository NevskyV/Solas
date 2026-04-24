using System.Diagnostics;

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
            foreach (var f in Engine.Context.EntityPool.FixedUpdatables)
                f?.FixedUpdate();

            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }
        
        if (steps == MaxFixedStepsPerTick)
            _accumulator = 0;
        
        Time.Alpha = _accumulator / Time.FixedDeltaTime;
        foreach (var u in Engine.Context.EntityPool.Updatables)
            u?.Update();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
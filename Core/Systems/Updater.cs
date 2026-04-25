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
    public float TargetFrameTime { get; set; } =  60.0f;
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
        //test
        /*Stopwatch sw = Stopwatch.StartNew();

        int frames = 0;
        while (frames < 1000)
        {
            sw.Restart();

            Tick();

            sw.Stop();

            Console.WriteLine($"Frame: {sw.Elapsed.TotalMilliseconds:F4} ms");

            frames++;
        }*/
        while (e.State != GameState.None)
        {
            double startTicks = _stopwatch.ElapsedTicks;

            Tick();

            double targetTicks = (1.0 / TargetFrameTime * Stopwatch.Frequency);
            double elapsedTicks = _stopwatch.ElapsedTicks - startTicks;

            double remaining = targetTicks - elapsedTicks;

            if (remaining > 0)
            {
                int ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                if (ms > 0)
                    Thread.Sleep(ms);
                while (e.State != GameState.None && (_stopwatch.ElapsedTicks - startTicks) < targetTicks)
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
            var fixedUpdatables = _entityPool.FixedUpdateRunners;
            for (int i = 0; i < fixedUpdatables.Count; i++)
            {
                fixedUpdatables[i].Run();
            }

            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }
        
        _accumulator = Math.Min(_accumulator, Time.FixedDeltaTime);
        
        double invFixedDelta = 1.0 / Time.FixedDeltaTime;
        Time.Alpha = _accumulator * invFixedDelta;
        
        var updatables = _entityPool.UpdateRunners;
        for (int i = 0; i < updatables.Count; i++)
        {
            updatables[i].Run();
        }
        
        var lateUpdatables = _entityPool.LateUpdateRunners;
        for (int i = 0; i < lateUpdatables.Count; i++)
        {
            lateUpdatables[i].Run();
        }
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
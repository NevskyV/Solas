using System.Diagnostics;
using System.Runtime.InteropServices;
using Solas.Containers;
using Solas.Enums;
using Solas.Interfaces;

namespace Solas.Systems;

internal class UpdateSystem
{
    internal readonly List<IUpdateSystem> FixedUpdateSystems = [];
    internal readonly List<IUpdateSystem> LateUpdateSystems = [];
    internal readonly List<IUpdateSystem> UpdateSystems = [];

    private const int MaxFixedStepsPerTick = 5;
    private readonly Stopwatch _stopwatch = new();
    private readonly double _tickToSeconds = 1.0 / Stopwatch.Frequency;
    private double _accumulator;
    private double _previousTime;

    private bool _isRunning;
    private EntityPool EntityPool => EngineContext.EntityPool;

    private double _debugFrameAccumulator;
    private int _debugFrameCount;

    internal void Start()
    {
        if (_isRunning) return;

        _stopwatch.Restart();
        _previousTime = _stopwatch.Elapsed.TotalSeconds;
        _accumulator = 0;
        _isRunning = true;

        var injectAction = EngineContext.SpacePool.InjectPoolsInUpdateRunners;
        while (Engine.State != GameState.None)
        {
            double startTicks = _stopwatch.ElapsedTicks;

            injectAction(CollectionsMarshal.AsSpan(EntityPool.UpdateRunners));
            injectAction(CollectionsMarshal.AsSpan(EntityPool.FixedUpdateRunners));
            injectAction(CollectionsMarshal.AsSpan(EntityPool.LateUpdateRunners));

            long tickStartTicks = _stopwatch.ElapsedTicks;
            Tick();
            long tickEndTicks = _stopwatch.ElapsedTicks;

            double tickMs = (tickEndTicks - tickStartTicks) * 1000.0 / Stopwatch.Frequency;

            if (WorldContext.CoreSettings.IsProfilingEnabled)
            {
                _debugFrameAccumulator += tickMs;
                _debugFrameCount++;

                if (_debugFrameAccumulator >= 1000.0)
                {
                    double avgMs = _debugFrameAccumulator / _debugFrameCount;
                    double potentialFps = 1000.0 / avgMs;
                    Debug.WriteLine($"[Profiler] Tick Avg Time: {avgMs:F3} ms | Max Uncapped FPS: {potentialFps:F0}");

                    _debugFrameAccumulator = 0;
                    _debugFrameCount = 0;
                }
            }

            if (!WorldContext.CoreSettings.IsProfilingEnabled)
            {
                var targetTicks = 1.0 / WorldContext.CoreSettings.TargetFrameTime * Stopwatch.Frequency;
                var elapsedTicks = _stopwatch.ElapsedTicks - startTicks;

                var remaining = targetTicks - elapsedTicks;

                if (remaining > 0)
                {
                    var ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                    if (ms > 0)
                        Thread.Sleep(ms);
                    while (Engine.State != GameState.None && _stopwatch.ElapsedTicks - startTicks < targetTicks)
                        Thread.SpinWait(10);
                }
            }
        }

        _isRunning = false;
    }

    private void Tick()
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
            for (var i = 0; i < FixedUpdateSystems.Count; i++)
                EngineContext.SpacePool.RunUpdateSystemInAllSpaces(FixedUpdateSystems[i]);
            var fixedUpdatables = EntityPool.FixedUpdateRunners;
            for (var i = 0; i < fixedUpdatables.Count; i++) fixedUpdatables[i].Run();

            _accumulator -= Time.FixedDeltaTime;
            steps++;
        }

        _accumulator = Math.Min(_accumulator, Time.FixedDeltaTime);

        var invFixedDelta = 1.0 / Time.FixedDeltaTime;
        Time.Alpha = _accumulator * invFixedDelta;

        for (var i = 0; i < UpdateSystems.Count; i++)
            EngineContext.SpacePool.RunUpdateSystemInAllSpaces(UpdateSystems[i]);
        var updatables = EntityPool.UpdateRunners;
        for (var i = 0; i < updatables.Count; i++) updatables[i].Run();

        for (var i = 0; i < LateUpdateSystems.Count; i++)
            EngineContext.SpacePool.RunUpdateSystemInAllSpaces(LateUpdateSystems[i]);
        var lateUpdatables = EntityPool.LateUpdateRunners;
        for (var i = 0; i < lateUpdatables.Count; i++) lateUpdatables[i].Run();
    }

    internal void Stop()
    {
        _stopwatch.Stop();
    }
}
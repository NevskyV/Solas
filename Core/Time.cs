namespace Solas;

public static class Time
{
    public static double DeltaTime { get; internal set; }
    public static double FixedDeltaTime { get; set; } = 0.02;
    public static double TimeScale { get; set; } = 1.0;
    public static double Alpha { get; internal set; }
}
using System.Diagnostics;

public static class SimulationEventTracker
{
    public static Simulation TimeSource;
    public static List<(TimeSpan, string, StackTrace)> Events = new();

    public static void Log(string str)
    {
        Events.Add((TimeSource.CurrentTime, str, new StackTrace()));
    }
}
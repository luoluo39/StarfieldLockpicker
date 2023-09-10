using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Diagnostics;

Console.WriteLine("233");
BenchmarkRunner.Run<BenchmarkTimer>();

public class BenchmarkTimer
{
    private CancellationTokenSource cts = new();
    [Benchmark]
    public void Wait05()
    {
        PrecisionTimer.Wait(TimeSpan.FromMilliseconds(0.5), cts.Token);
    }

    [Benchmark]
    public void Wait15()
    {
        PrecisionTimer.Wait(TimeSpan.FromMilliseconds(1.5), cts.Token);
    }

    [Benchmark]
    public void Wait6()
    {
        PrecisionTimer.Wait(TimeSpan.FromMilliseconds(6), cts.Token);
    }

    [Benchmark]
    public void Wait20()
    {
        PrecisionTimer.Wait(TimeSpan.FromMilliseconds(20), cts.Token);
    }
    [Benchmark]
    public void Sleep20()
    {
        Thread.Sleep(20);
    }
    [Benchmark]
    public void Sleep0()
    {
        Thread.Sleep(0);
    }
}

public readonly struct PrecisionTimer
{
    private readonly long _end;
    public PrecisionTimer(TimeSpan time)
    {
        _end = GetEndTime(time);
    }

    public void Wait()
    {
        WaitUntil(_end);
    }

    private static long GetEndTime(TimeSpan time)
    {
        return Stopwatch.GetTimestamp() + time.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
    }

    public static void Wait(TimeSpan time)
    {
        var end = GetEndTime(time);
        WaitUntil(end);
    }

    public static void Wait(TimeSpan time, CancellationToken cancellationToken)
    {
        var end = GetEndTime(time);
        WaitUntil(end, cancellationToken);
    }

    private static void WaitUntil(long timestamp)
    {
        if (Stopwatch.GetTimestamp() >= timestamp)
            return;

        var t0 = timestamp - Stopwatch.Frequency * 1 / 1000;
        var msToWait = (timestamp - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency - 13;
        if (msToWait > 0)
            Thread.Sleep((int)msToWait);

        while (Stopwatch.GetTimestamp() < t0)
            Thread.Sleep(0);

        while (Stopwatch.GetTimestamp() < timestamp)
            ;
    }

    private static void WaitUntil(long timestamp, CancellationToken cancellationToken)
    {
        if (Stopwatch.GetTimestamp() >= timestamp || cancellationToken.IsCancellationRequested)
            return;

        var t0 = timestamp - Stopwatch.Frequency * 1 / 1000;
        var msToWait = (timestamp - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency - 13;
        if (msToWait > 0 && !cancellationToken.IsCancellationRequested)
            Thread.Sleep((int)msToWait);

        while (Stopwatch.GetTimestamp() < t0 && !cancellationToken.IsCancellationRequested)
            Thread.Sleep(0);

        while (Stopwatch.GetTimestamp() < timestamp && !cancellationToken.IsCancellationRequested)
            ;
    }
}
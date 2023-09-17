using BenchmarkDotNet.Attributes;

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
    public void Delay20()
    {
        Task.Delay(20).Wait();
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
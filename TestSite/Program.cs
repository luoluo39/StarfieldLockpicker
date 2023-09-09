// See https://aka.ms/new-console-template for more information

using System.Collections.Immutable;
using StarfieldLockpicker.Core;
using TestSite;

Console.WriteLine("Hello, World!");

var b = (Bitmap)Image.FromFile(@"C:\Users\24580\Downloads\15ROnfk.jpeg");
AppConfig.DefaultScreenWidth = b.Width;
AppConfig.DefaultScreenHeight = b.Height;
AppConfig.Instance = AppConfig.LoadOrCreateConfig(null) ?? throw new NullReferenceException();
var locks = Enumerable.Range(0, 4).Select(i => ImageSe.GradGetLockShape32(b, i)).ToArray();
//var keys = Enumerable.Repeat(b, 1).Select(ImageSe.GetKeyShape32).ToArray();
var keys = Enumerable.Repeat(1U, 12).ToArray();

locks[0] = 0b11111111111101111111110111111101;
locks[1] = 0b01111111010111111111110111111111;
locks[2] = 0b11111111110101110111010111111101;
locks[3] = 0b11111111111111110101110101111101;

keys[0] = 0b00100000000000100000000010100000;
keys[1] = 0b00100000000000000000000010000000;
keys[2] = 0b00000000000000000000100010000000;
keys[3] = 0b00001010001010000000000000000000;
keys[4] = 0b10101000000000000000000000000000;
keys[5] = 0b00000000000000001000000000000010;
keys[6] = 0b00001000000000001000000000000000;
keys[7] = 0b00001000000010000000000000000000;
keys[8] = 0b00000000000000000000000000001000;
keys[9] = 0b00000010001000000000000000000000;
keys[10] = 0b00000000100000000000000000000000;
keys[11] = 0b00000000000000001000000010000000;

int t = 0;
while (true)
{
    t++;
    TimeSpan ate;
    bool rte = false;
    bool withBreak = false;
    Simulation s = new()
    {
        KeyShapes = keys.ToImmutableArray(),
        LockShapes = locks.ToImmutableArray()
    };
    SimulationEventTracker.TimeSource = s;
    try
    {
        await UnlockTask.RunAsync(s, s.SimulateCts.Token);
        rte = true;
    }
    catch (TaskCanceledException)
    {
        //Console.WriteLine($"Canceled, actual exit time: {s.CurrentTime}");
        rte = true;
        withBreak = true;
    }
    catch (OperationCanceledException)
    {
        //Console.WriteLine($"Canceled, actual exit time: {s.CurrentTime}");
        rte = true;
        withBreak = true;
    }
    catch (TerminatingException)
    {
        //Console.WriteLine($"Canceled, actual exit time: {s.CurrentTime}");
    }
    //catch (TerminatingException)
    //{
    //    Console.WriteLine($"{t}:Terminated");
    //}
    finally
    {
        ate = s.CurrentTime;
    }
    //Console.WriteLine($"Time: {s.CurrentTime}");

    //Console.WriteLine("Event Track:");
    //foreach (var (time, str, stack) in SimulationEventTracker.Events)
    //{
    //    Console.WriteLine($"{time}: {str}");
    //}


    if (!rte)
    {
        foreach (var valueTuple in SimulationEventTracker.Events)
        {
            Console.WriteLine($"[{valueTuple.Item1}] {valueTuple.Item2}");
        }

        Console.ReadLine();
    }
    else
    {
        var unDisposed = s.AllSnaps.Where(t => !t.Disposed).ToArray();
        Console.WriteLine($"Leak snap: {s.AllSnaps.Count(static t => !t.Disposed)}");

        var snap = s.MakeSnap();
        if (!withBreak && snap.Locks.Any(t => t != uint.MaxValue))
        {
            foreach (var valueTuple in SimulationEventTracker.Events)
            {
                Console.WriteLine($"[{valueTuple.Item1}] {valueTuple.Item2}");
            }

            Console.WriteLine("Not even completed!");
            Console.ReadLine();
        }

        if ((t % 1024) == 0)
            Console.Write('1');
    }
    SimulationEventTracker.Events.Clear();


    //Console.ReadLine();
}
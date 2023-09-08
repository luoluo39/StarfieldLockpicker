using System.Collections.Immutable;
using StarfieldLockpicker.Core;

public class Simulation : ICoreInterface
{
    public ImmutableArray<uint> LockShapes;
    public ImmutableArray<uint> KeyShapes;
    public SimulationArguments Arguments = new();
    public AppArgument AppArgument = new();
    public SortedList<TimeSpan, SimulationEvent> Events = new();
    public SortedList<TimeSpan, Snap> TimedSnaps = new();
    public HashSet<Snap> AllSnaps = new();
    public TimeSpan CurrentTime = TimeSpan.Zero;

    public CancellationTokenSource SimulateCts = new();

    public void DoRandomCancellation(CancellationToken cancellationToken)
    {
        if (Arguments.CheckCancellation)
        {
            SimulationEventTracker.Log("Cancellation");
            SimulateCts.Cancel();

            if (cancellationToken.IsCancellationRequested || cancellationToken.CanBeCanceled)
            {
                throw new TaskCanceledException();
            }
        }
    }

    public Snap MakeSnap()
    {
        var (latestTime, latestSnap) = TimedSnaps.LastOrDefault(t => t.Key <= CurrentTime);
        latestTime = default;
        latestSnap = default;

        var selectedKey = 0;
        var currentLevel = 0;
        Span<uint> keys = stackalloc uint[KeyShapes.Length];
        Span<uint> locks = stackalloc uint[LockShapes.Length];
        if (latestSnap is not null)
        {
            selectedKey = latestSnap.SelectedKey;
            currentLevel = latestSnap.CurrentLevel;
            latestSnap.Keys.CopyTo(keys);
            latestSnap.Locks.CopyTo(locks);
        }
        else
        {
            KeyShapes.CopyTo(keys);
            LockShapes.CopyTo(locks);
        }

       
        foreach (var (_, e) in Events.SkipWhile(t => t.Key <= latestTime).TakeWhile(t => t.Key <= CurrentTime))
        {
            switch (e)
            {
                case SimulationEventCommandFinish simulationEventCommandFinish:
                    switch (simulationEventCommandFinish.InputCommand.Command)
                    {
                        case InputCommand.Next:
                            do
                            {
                                selectedKey = (selectedKey + 1) % keys.Length;
                            } while (keys[selectedKey] == 0);
                            break;
                        case InputCommand.Previous:
                            do
                            {
                                selectedKey = (selectedKey + keys.Length - 1) % keys.Length;
                            } while (keys[selectedKey] == 0);
                            break;
                        case InputCommand.RotateClockwise:
                            keys[selectedKey] = uint.RotateLeft(keys[selectedKey], 1);
                            break;
                        case InputCommand.RotateAntiClockwise:
                            keys[selectedKey] = uint.RotateRight(keys[selectedKey], 1);
                            break;
                        case InputCommand.Insert:
                            if ((locks[currentLevel] & keys[selectedKey]) != 0)
                                break;
                            locks[currentLevel] |= keys[selectedKey];
                            if (locks[currentLevel] == uint.MaxValue)
                            {
                                currentLevel++;
                            }
                            keys[selectedKey] = 0U;
                            do
                            {
                                selectedKey = (selectedKey + 1) % keys.Length;
                            } while (keys[selectedKey] == 0);
                            break;
                    }
                    break;
            }
        }

        return new(this, selectedKey, currentLevel, keys.ToImmutableArray(), locks.ToImmutableArray());
    }

    public Task<IFullImage> CaptureFullScreenAsync(CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        CurrentTime += TimeSpan.FromMilliseconds(2);
        var snap = MakeSnap();
        TimedSnaps.TryAdd(CurrentTime, snap);
        AllSnaps.Add(snap);
        SimulationEventTracker.Log($"Sim: making full snap, sk{snap.SelectedKey},cl{snap.CurrentLevel}");
        CurrentTime += TimeSpan.FromMilliseconds(2);
        return Task.FromResult((IFullImage)snap);
    }

    public Task<ILockImage> CaptureLockImageAsync(CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        CurrentTime += TimeSpan.FromMilliseconds(2);

        var snap = MakeSnap();
        TimedSnaps.TryAdd(CurrentTime, snap);
        AllSnaps.Add(snap);
        SimulationEventTracker.Log($"Sim: making lock snap, sk{snap.SelectedKey},cl{snap.CurrentLevel}");
        CurrentTime += TimeSpan.FromMilliseconds(2);
        return Task.FromResult((ILockImage)snap);
    }

    public Task<IKeySelectionImage> CaptureKeyImageAsync(CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        CurrentTime += TimeSpan.FromMilliseconds(2);

        var snap = MakeSnap();
        TimedSnaps.TryAdd(CurrentTime, snap);
        AllSnaps.Add(snap);
        SimulationEventTracker.Log($"Sim: making key snap, sk{snap.SelectedKey},cl{snap.CurrentLevel}");
        CurrentTime += TimeSpan.FromMilliseconds(2);
        return Task.FromResult((IKeySelectionImage)snap);
    }

    public Task SendCommandAsync(InputCommand command, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        CurrentTime += TimeSpan.FromMilliseconds(AppArgument.InputDelay + Arguments.RandomTimeDelayOffset);
        var cmd = new SimulationEventCommandInput { Command = command };
        Events.Add(CurrentTime, cmd);


        if (!Arguments.CheckEatKey)
        {
            var finish = new SimulationEventCommandFinish { InputCommand = cmd };

            var finishTime = CurrentTime;
            if (Arguments.CheckLargeLag)
                finishTime += TimeSpan.FromMilliseconds(Arguments.RandomTimeLargeLag);
            else if (Arguments.CheckSmallLag)
                finishTime += TimeSpan.FromMilliseconds(Arguments.RandomTimeSmallLag);
            else
                finishTime += TimeSpan.FromMilliseconds(Arguments.RandomTimeReact);
            Events.Add(finishTime, finish);

            SimulationEventTracker.Log($"Sim: sent command {command}, ft{finishTime}");
        }
        else
        {
            SimulationEventTracker.Log($"Sim: eat command {command}");
        }
        

        return Task.CompletedTask;
    }

    public Task SendCommandsAsync(InputCommand[] commands, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        foreach (var command in commands)
        {
            SendCommandAsync(command, cancellationToken);
        }
        return Task.CompletedTask;
    }

    public Task RepeatCommandsAsync(InputCommand command, int times, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        for (int i = 0; i < times; i++)
        {
            SendCommandAsync(command, cancellationToken);
        }
        return Task.CompletedTask;
    }

    public Task Delay(DelayReason reason, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        SimulationEventTracker.Log($"Sim: delaying");
        CurrentTime += TimeSpan.FromMilliseconds(AppArgument.GetDelayTime(reason) + Arguments.RandomTimeDelayOffset);
        SimulationEventTracker.Log($"Sim: delayed");
        return Task.CompletedTask;
    }

    public async Task<bool> WaitUntil(Func<Task<bool>> condition, DelayReason reason, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);
        SimulationEventTracker.Log($"Sim: begin wait until");
        var beg = CurrentTime;
        while (true)
        {
            await Delay(reason, cancellationToken);
            if (await condition())
            {
                SimulationEventTracker.Log($"Sim: wait until success");
                return true;
            }

            if ((CurrentTime - beg).TotalMilliseconds > AppArgument.GetWaitTimeout(reason))
            {
                SimulationEventTracker.Log($"Sim: wait until timeout");
                return false;
            }
        }
    }

    public async Task<(bool, TResult)> WaitUntil<TResult>(Func<Task<(bool, TResult)>> condition, DelayReason reason, CancellationToken cancellationToken)
    {
        DoRandomCancellation(cancellationToken);

        SimulationEventTracker.Log($"Sim: begin arg wait until");
        var beg = CurrentTime;
        while (true)
        {
            await Delay(reason, cancellationToken);
            var (s, r) = await condition();
            if (s)
            {
                SimulationEventTracker.Log($"Sim: wait until success");
                return (s, r);
            }

            if ((CurrentTime - beg).TotalMilliseconds > AppArgument.GetWaitTimeout(reason))
            {
                SimulationEventTracker.Log($"Sim: wait until timeout");
                return (false, r);
            }
        }
    }

    public void ConsoleError(string str)
    {
        SimulationEventTracker.Log($"Error: {str}");
    }

    public void ConsoleWarning(string str)
    {
        SimulationEventTracker.Log($"Warning: {str}");
    }

    public void ConsoleInfo(string str)
    {
        SimulationEventTracker.Log($"Info: {str}");
    }

    public void ConsoleDebug(string str)
    {
        SimulationEventTracker.Log($"Debug: {str}");
    }

    public double MseThr => Arguments.MseThr;
}
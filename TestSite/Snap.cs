using System.Collections.Immutable;
using StarfieldLockpicker.Core;

public sealed class Snap : IFullImage
{
    public int SelectedKey { get; }
    public int CurrentLevel { get; }
    public ImmutableArray<uint> Keys { get; }
    public ImmutableArray<uint> Locks { get; }
    public Simulation Simulation { get; }
    public bool Disposed { get; private set; }

    public Snap(Simulation simulation, int selectedKey, int currentLevel, ImmutableArray<uint> keys,
        ImmutableArray<uint> locks, bool disposed = false)
    {
        SelectedKey = selectedKey;
        CurrentLevel = currentLevel;
        Keys = keys;
        Locks = locks;
        Simulation = simulation;
        Disposed = disposed;
    }

    public void Dispose()
    {
        if (Disposed)
            SimulationEventTracker.Log("Trying to dispose disposed object");
        Disposed = true;
    }

    public double KeyAreaMseWith(IKeySelectionImage other)
    {
        if (other is Snap snap)
        {
            var mse = Simulation.Arguments.RandomMseSameImage;

            if (!snap.Keys.SequenceEqual(Keys))
            {
                mse += Simulation.Arguments.RandomMseRotatedKey;
            }
            if (snap.SelectedKey != SelectedKey)
            {
                mse += Simulation.Arguments.RandomMseDifferentKey;
            }

            return mse;
        }

        throw new NotSupportedException();
    }

    public uint GetLockShape(int layer)
    {
        return Locks[layer];
    }

    public uint GetKeyShape()
    {
        return Keys[SelectedKey];
    }
}
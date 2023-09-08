using StarfieldLockpicker.Core;

namespace StarfieldLockpicker;

public class PooledWrappedBitmap : IFullImage
{
    public PooledWrappedBitmap(WrappedBitmap inner, BitmapPool pool)
    {
        Inner = inner;
        Pool = pool;
    }

    public WrappedBitmap Inner { get; }
    public BitmapPool Pool { get; }
    public bool Disposed { get; private set; }

    public void Dispose()
    {
        if (!Disposed)
        {
            Pool.Return(Inner);
            Disposed = true;
        }
    }

    public double KeyAreaMseWith(IKeySelectionImage other)
    {
        return Inner.KeyAreaMseWith(other);
    }

    public uint GetLockShape(int layer)
    {
        return Inner.GetLockShape(layer);
    }

    public uint GetKeyShape()
    {
        return Inner.GetKeyShape();
    }
}
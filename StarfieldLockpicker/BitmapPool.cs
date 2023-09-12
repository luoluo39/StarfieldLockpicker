using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;

namespace StarfieldLockpicker;

public class BitmapPool
{
    public AppConfig Config { get; }

    private readonly ConcurrentDictionary<Size, ConcurrentBag<WrappedBitmap>> bitmaps = new();
    private readonly ConcurrentBag<WrappedBitmap> allocated = new();

    public BitmapPool(AppConfig config)
    {
        Config = config;
    }

    public PooledWrappedBitmap Rent(Rectangle bitmapRect)
    {
        var bag = bitmaps.GetOrAdd(bitmapRect.Size, _ => new());
        if (!bag.TryTake(out var result))
        {
            result = new WrappedBitmap(new Bitmap(bitmapRect.Width, bitmapRect.Height, PixelFormat.Format32bppArgb),
                Config, bitmapRect);
            allocated.Add(result);
        }
        return new(result, this);
    }

    public void Return(WrappedBitmap bitmap)
    {
        var bag = bitmaps.GetOrAdd(bitmap.Bitmap.Size, _ => new());
        bag.Add(bitmap);
    }

    public void ReleaseAll()
    {
        foreach (var bitmap in allocated)
        {
            bitmap.Dispose();
        }
        allocated.Clear();
        bitmaps.Clear();
    }
}
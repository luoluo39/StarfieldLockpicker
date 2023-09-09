using System.Collections.Concurrent;
using System.Drawing.Imaging;

namespace StarfieldLockpicker;

public class BitmapPool
{
    public Rectangle BitmapRect { get; }
    public AppConfig Config { get; }

    private readonly ConcurrentBag<WrappedBitmap> bitmaps = new();
    private readonly ConcurrentBag<WrappedBitmap> allocated = new();

    public BitmapPool(Rectangle bitmapRect, AppConfig config)
    {
        BitmapRect = bitmapRect;
        Config = config;
    }

    public PooledWrappedBitmap Rent()
    {
        if (!bitmaps.TryTake(out var result))
        {
            result = new WrappedBitmap(new Bitmap(BitmapRect.Width, BitmapRect.Height, PixelFormat.Format32bppArgb),
                Config, BitmapRect);
            allocated.Add(result);
        }
        return new(result, this);
    }

    public void Return(WrappedBitmap bitmap)
    {
        bitmaps.Add(bitmap);
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
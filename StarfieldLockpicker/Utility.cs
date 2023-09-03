using System.Drawing.Imaging;
using System.Numerics;
using PInvoke;

namespace StarfieldLockpicker;

public static class Utility
{
    public static unsafe void MessageLoop(CancellationToken ct)
    {

        User32.MSG msg = default;
        while (!ct.IsCancellationRequested)
        {
            if (User32.PeekMessage(&msg, nint.Zero, 0, 0, User32.PeekMessageRemoveFlags.PM_REMOVE))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);
            }
        }
    }

    public static Bitmap CaptureScreen()
    {
        var captureRectangle = Screen.AllScreens[0].Bounds;
        var captureBitmap = new Bitmap(captureRectangle.Size.Width, captureRectangle.Size.Height, PixelFormat.Format32bppArgb);
        var captureGraphics = Graphics.FromImage(captureBitmap);
        captureGraphics.CopyFromScreen(captureRectangle.Left, captureRectangle.Top, 0, 0, captureRectangle.Size);
        return captureBitmap;
    }

    public static void ExtractIndexes(ulong number, Span<int> index, ReadOnlySpan<int> dims)
    {
        if (index.Length != dims.Length)
            throw new ArgumentException();

        ulong mul = 1;
        for (var i = 0; i < index.Length; i++)
        {
            var count = (ulong)dims[i];
            index[i] = (int)(number / mul % count);
            mul *= count;
        }
    }
    public static bool CheckAllBitSet(ReadOnlySpan<uint> playground)
    {
        foreach (var item in playground)
        {
            if (item != uint.MaxValue)
                return false;
        }
        return true;
    }

    public static bool CheckAllBitSet(ReadOnlySpan<ulong> playground)
    {
        foreach (var item in playground)
        {
            if (item != ulong.MaxValue)
                return false;
        }
        return true;
    }

    public static float CalculateMaxColor(Bitmap bitmap, Vector2 center, float radius, bool print = false)
    {
        float max = -1;

        var x0 = (int)float.Floor(center.X - radius);
        var y0 = (int)float.Floor(center.Y - radius);
        var x1 = (int)float.Ceiling(center.X + radius);
        var y1 = (int)float.Ceiling(center.Y + radius);

        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                if (IsInsideCircle(center, new(x, y), radius))
                {
                    var pixelColor = bitmap.GetPixel(x, y);
                    var grayValue = 0.3f * pixelColor.G + 0.7f * pixelColor.B;
                    max = float.Max(grayValue, max);
                }
            }
        }
        if (print)
            Console.WriteLine(max);
        return max;
    }

    public static bool IsInsideCircle(Vector2 center, Vector2 pos, float radius)
    {
        return Vector2.DistanceSquared(center, pos) <= radius * radius;
    }

}
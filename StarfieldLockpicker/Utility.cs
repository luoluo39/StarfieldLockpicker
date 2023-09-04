using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using PInvoke;

namespace StarfieldLockpicker;

public static class Utility
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static Vector2 ScalePosition(Vector2 value)
    {
        var config = AppConfig.Instance;
        return value * config.ScreenSizeVector / new Vector2(config.ReferenceResolutionWidth, config.ReferenceResolutionHeight);
    }

    public static float ScaleRadius(float value)
    {
        return value * AppConfig.Instance.ScreenHeight / AppConfig.Instance.ReferenceResolutionHeight;
    }

    public static int ScaleWidth(int value)
    {
        return value / AppConfig.Instance.ReferenceResolutionHeight * AppConfig.Instance.ScreenHeight +
               (AppConfig.Instance.ScreenWidth - AppConfig.Instance.ReferenceResolutionWidth /
                   AppConfig.Instance.ReferenceResolutionHeight * AppConfig.Instance.ScreenHeight) / 2;
    }

    public static int ScaleHeight(int value)
    {
        return value * AppConfig.Instance.ScreenHeight / AppConfig.Instance.ReferenceResolutionHeight;
    }

    public static Bitmap CaptureScreen(int display)
    {
        var captureRectangle = Screen.AllScreens[display].Bounds;
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

    public static double CalculateKeyAreaMSE(Bitmap bmp1, Bitmap bmp2)
    {
        if (bmp1.Size != bmp2.Size)
            throw new ArgumentException("Bitmaps must have the same dimensions.");

        int width = bmp1.Width;
        int height = bmp1.Height;
        double mse = 0;

        BitmapData data1 = bmp1.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData data2 = bmp2.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        //1333,130,494,744 on 1080p

        var config = AppConfig.Instance;

        var x0 = ScaleWidth(config.KeyAreaX0);
        var y0 = ScaleHeight(config.KeyAreaY0);

        var x1 = ScaleWidth(config.KeyAreaX0 + config.KeyAreaWidth);
        var y1 = ScaleHeight(config.KeyAreaY0 + config.KeyAreaHeight);

        unsafe
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var ptr1 = (byte*)data1.Scan0 + (data1.Stride * y + x * 4);
                    var ptr2 = (byte*)data2.Scan0 + (data2.Stride * y + x * 4);
                    for (int i = 1; i < 4; i++) // 4 bytes per pixel (ARGB)
                    {
                        int diff = ptr1[i] - ptr2[i];
                        mse += diff * diff;
                    }
                }
            }
        }

        bmp1.UnlockBits(data1);
        bmp2.UnlockBits(data2);

        double msePerPixel = mse / ((x1 - x0) * (y1 - y0) * 3.0);
        return msePerPixel;
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
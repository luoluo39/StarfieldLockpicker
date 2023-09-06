using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace StarfieldLockpicker;

public static class Utility
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static Vector2 ScalePosition(Vector2 value)
    {
        return new(ScaleFloatPositionX(value.X), ScaleFloatPositionY(value.Y));
    }

    public static float ScaleRadius(float value)
    {
        //narrower than 16:9
        if ((float)AppConfig.Instance.ScreenWidth / AppConfig.Instance.ScreenHeight < 16f / 9f)
            return value * AppConfig.Instance.ScreenWidth / AppConfig.Instance.ReferenceResolutionWidth;
        return value * AppConfig.Instance.ScreenHeight / AppConfig.Instance.ReferenceResolutionHeight;

    }

    public static float ScaleFloatPositionX(float value)
    {
        //narrower than 16:9
        if ((float)AppConfig.Instance.ScreenWidth / AppConfig.Instance.ScreenHeight < 16f / 9f)
            return value * AppConfig.Instance.ScreenWidth / AppConfig.Instance.ReferenceResolutionWidth;

        return value * AppConfig.Instance.ScreenHeight / AppConfig.Instance.ReferenceResolutionHeight +
           (AppConfig.Instance.ScreenWidth - (float)AppConfig.Instance.ReferenceResolutionWidth *
               AppConfig.Instance.ScreenHeight / AppConfig.Instance.ReferenceResolutionHeight) / 2;
    }

    public static float ScaleFloatPositionY(float value)
    { 
        //narrower than 16:9
        if ((float)AppConfig.Instance.ScreenWidth / AppConfig.Instance.ScreenHeight < 16f / 9f)
            return value * AppConfig.Instance.ScreenWidth / AppConfig.Instance.ReferenceResolutionWidth +
                   (AppConfig.Instance.ScreenHeight - (float)AppConfig.Instance.ReferenceResolutionHeight *
                       AppConfig.Instance.ScreenWidth / AppConfig.Instance.ReferenceResolutionWidth) / 2;

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

    public static Bitmap CaptureScreenArea(int display, Rectangle rect)
    {
        var bounds = Screen.AllScreens[display].Bounds;
        var w = Math.Min(rect.Width, bounds.Width - rect.Left);
        var h = Math.Min(rect.Height, bounds.Height - rect.Top);
        var captureRectangle = new Rectangle(bounds.Left, bounds.Top, w, h);

        var captureBitmap = new Bitmap(rect.Size.Width, rect.Size.Height, PixelFormat.Format32bppArgb);
        var captureGraphics = Graphics.FromImage(captureBitmap);
        captureGraphics.CopyFromScreen(captureRectangle.Left, captureRectangle.Top, 0, 0, captureRectangle.Size);
        return captureBitmap;
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

        var x0 = (int)ScaleFloatPositionX(config.KeyAreaX0);
        var y0 = (int)ScaleFloatPositionY(config.KeyAreaY0);

        var x1 = (int)ScaleFloatPositionX(config.KeyAreaX0 + config.KeyAreaWidth);
        var y1 = (int)ScaleFloatPositionY(config.KeyAreaY0 + config.KeyAreaHeight);

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
                        //They may lied. is it actually BGRA?
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

    public static float CalculateMaxB(Bitmap bitmap, Vector2 center, float radius)
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
                    max = float.Max(pixelColor.R, max);
                    max = float.Max(pixelColor.G, max);
                    max = float.Max(pixelColor.B, max);
                }
            }
        }
        return max / 255f;
    }

    public static bool IsInsideCircle(Vector2 center, Vector2 pos, float radius)
    {
        return Vector2.DistanceSquared(center, pos) <= radius * radius;
    }

    public static void ConsoleError(string str)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(str);
        Console.ResetColor();
    }

    public static void ConsoleWarning(string str)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(str);
        Console.ResetColor();
    }

    public static void ConsoleInfo(string str)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(str);
        Console.ResetColor();
    }

    public static void ConsoleDebug(string str)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(str);
        Console.ResetColor();
    }
}
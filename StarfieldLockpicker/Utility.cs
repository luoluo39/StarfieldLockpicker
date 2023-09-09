using System.Drawing.Imaging;
using System.Numerics;

namespace StarfieldLockpicker;

public static class Utility
{
    public static RectangleF TranslateRectangleF(RectangleF rectInReference, AppConfig? config = null)
    {
        config ??= AppConfig.Instance;

        var min = new Vector2(rectInReference.X, rectInReference.Y);
        var max = new Vector2(rectInReference.Right, rectInReference.Bottom);

        var tMin = TranslatePosition(min, config);
        var tMax = TranslatePosition(max, config);

        return new RectangleF(tMin.X, tMin.Y, tMax.X - tMin.X, tMax.Y - tMin.Y);
    }

    public static Rectangle TranslateRectangleCeiling(RectangleF rectInReference, AppConfig? config = null)
    {
        var translated = TranslateRectangleF(rectInReference, config);
        var tMin = new Point((int)translated.X, (int)translated.Y);
        var tMax = new Point((int)float.Ceiling(translated.Right), (int)float.Ceiling(translated.Bottom));

        return new Rectangle(tMin.X, tMin.Y, tMax.X - tMin.X, tMax.Y - tMin.Y);
    }

    public static Rectangle TranslateRectangleCeiling(Rectangle rectInReference, AppConfig? config = null)
    {
        var translated = TranslateRectangleF(rectInReference, config);
        var tMin = new Point((int)translated.X, (int)translated.Y);
        var tMax = new Point((int)float.Ceiling(translated.Right), (int)float.Ceiling(translated.Bottom));

        return new Rectangle(tMin.X, tMin.Y, tMax.X - tMin.X, tMax.Y - tMin.Y);
    }

    public static Vector2 TranslatePosition(Vector2 posInReference, AppConfig? config = null)
    {
        config ??= AppConfig.Instance;

        //translate pos from reference pos to (-1,1)
        var rx = (posInReference.X * 2 - config.ReferenceResolutionWidth) / config.ReferenceUIWidth;
        var ry = (posInReference.Y * 2 - config.ReferenceResolutionHeight) / config.ReferenceUIHeight;

        //translate pos from (-1,1) to screen pos
        var x = (rx * config.ScreenUIWidth + config.ScreenWidth) / 2;
        var y = (ry * config.ScreenUIHeight + config.ScreenHeight) / 2;

        return new Vector2(x, y);
    }

    public static float ScaleRadius(float value)
    {
        var config = AppConfig.Instance;
        return value * config.ScreenUIScale / config.ReferenceUIScale;
    }

    public static float TranslatePositionX(float value)
    {
        var config = AppConfig.Instance;
        var rx = (value * 2 - config.ReferenceResolutionWidth) / config.ReferenceUIWidth;
        var x = (rx * config.ScreenUIWidth + config.ScreenWidth) / 2;
        return x;
    }

    public static float TranslatePositionY(float value)
    {
        var config = AppConfig.Instance;
        var ry = (value * 2 - config.ReferenceResolutionHeight) / config.ReferenceUIHeight;
        var y = (ry * config.ScreenUIHeight + config.ScreenHeight) / 2;
        return y;
    }

    public static Bitmap CaptureScreen(int display)
    {
        var captureRectangle = Screen.AllScreens[display].Bounds;
        var captureBitmap = new Bitmap(captureRectangle.Size.Width, captureRectangle.Size.Height, PixelFormat.Format32bppArgb);
        using var captureGraphics = Graphics.FromImage(captureBitmap);
        captureGraphics.CopyFromScreen(captureRectangle.Left, captureRectangle.Top, 0, 0, captureRectangle.Size);
        return captureBitmap;
    }

    public static void CaptureScreen(Bitmap bitmap, int display)
    {
        var captureRectangle = Screen.AllScreens[display].Bounds;

        if (bitmap.Size != captureRectangle.Size)
            throw new ArgumentException(nameof(bitmap));

        using var captureGraphics = Graphics.FromImage(bitmap);
        captureGraphics.CopyFromScreen(captureRectangle.Left, captureRectangle.Top, 0, 0, captureRectangle.Size);
    }

    public static void CaptureScreenArea(Bitmap bitmap, int display, Rectangle rect)
    {
        if (bitmap.Size != rect.Size)
            throw new ArgumentException(nameof(bitmap));

        var bounds = Screen.AllScreens[display].Bounds;
        var w = Math.Min(rect.Width, bounds.Width - rect.X);
        var h = Math.Min(rect.Height, bounds.Height - rect.Y);
        var captureRectangle = new Rectangle(bounds.X + rect.X, bounds.Y + rect.Y, w, h);

        using var captureGraphics = Graphics.FromImage(bitmap);
        captureGraphics.CopyFromScreen(captureRectangle.Left, captureRectangle.Top, 0, 0, captureRectangle.Size);
    }

    public static double CalculateMSE(WrappedBitmap bmp1, WrappedBitmap bmp2, Rectangle rectangle)
    {
        double mse = 0;

        var point1 = new Point(rectangle.X - bmp1.ScreenSpaceBounds.X, rectangle.Y - bmp1.ScreenSpaceBounds.Y);
        var point2 = new Point(rectangle.X - bmp2.ScreenSpaceBounds.X, rectangle.Y - bmp2.ScreenSpaceBounds.Y);

        var data1 = bmp1.Bitmap.LockBits(
            new Rectangle(point1, rectangle.Size),
            ImageLockMode.ReadOnly, 
            PixelFormat.Format32bppArgb
            );

        var data2 = bmp2.Bitmap.LockBits(
            new Rectangle(point2, rectangle.Size), 
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb
            );

        unsafe
        {
            for (int y = 0; y < rectangle.Height; y++)
            {
                for (int x = 0; x < rectangle.Width; x++)
                {
                    var color1 = Color.FromArgb(*(int*)(data1.Scan0 + (data1.Stride * y + x * 4)));
                    var color2 = Color.FromArgb(*(int*)(data2.Scan0 + (data2.Stride * y + x * 4)));
                    var cv1 = new Vector3(color1.R, color1.G, color1.B);
                    var cv2 = new Vector3(color2.R, color2.G, color2.B);
                    mse += Vector3.DistanceSquared(cv1, cv2);
                }
            }
        }

        bmp1.Bitmap.UnlockBits(data1);
        bmp2.Bitmap.UnlockBits(data2);

        double msePerPixel = mse / (rectangle.Width * rectangle.Height * 3.0);
        return msePerPixel;
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

        var x0 = (int)TranslatePositionX(config.KeyAreaX0);
        var y0 = (int)TranslatePositionY(config.KeyAreaY0);

        var x1 = (int)TranslatePositionX(config.KeyAreaX0 + config.KeyAreaWidth);
        var y1 = (int)TranslatePositionY(config.KeyAreaY0 + config.KeyAreaHeight);

        unsafe
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var color1 = Color.FromArgb(*(int*)(data1.Scan0 + (data1.Stride * y + x * 4)));
                    var color2 = Color.FromArgb(*(int*)(data2.Scan0 + (data2.Stride * y + x * 4)));
                    var cv1 = new Vector3(color1.R, color1.G, color1.B);
                    var cv2 = new Vector3(color2.R, color2.G, color2.B);
                    mse += Vector3.DistanceSquared(cv1, cv2);
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
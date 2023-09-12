using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

namespace StarfieldLockpicker;

public static class Utility
{
    public static RectangleF TranslateRectangleF(this AppConfig config, RectangleF rectInReference)
    {
        var min = new Vector2(rectInReference.X, rectInReference.Y);
        var max = new Vector2(rectInReference.Right, rectInReference.Bottom);

        var tMin = config.TranslatePosition(min);
        var tMax = config.TranslatePosition(max);

        return RectangleF.FromLTRB(tMin.X, tMin.Y, tMax.X, tMax.Y);
    }

    public static Rectangle TranslateRectangleCeiling(this AppConfig config, RectangleF rectInReference)
    {
        var translated = config.TranslateRectangleF(rectInReference);
        var tMin = new Point((int)translated.X, (int)translated.Y);
        var tMax = new Point((int)float.Ceiling(translated.Right), (int)float.Ceiling(translated.Bottom));

        return Rectangle.FromLTRB(tMin.X, tMin.Y, tMax.X, tMax.Y);
    }

    public static Rectangle TranslateRectangleCeiling(this AppConfig config, Rectangle rectInReference)
    {
        var translated = config.TranslateRectangleF(rectInReference);
        var tMin = new Point((int)translated.X, (int)translated.Y);
        var tMax = new Point((int)float.Ceiling(translated.Right), (int)float.Ceiling(translated.Bottom));

        return Rectangle.FromLTRB(tMin.X, tMin.Y, tMax.X, tMax.Y);
    }

    public static Vector2 TranslatePosition(this AppConfig config, Vector2 posInReference)
    {
        return Vector2.Transform(posInReference, config.ClientMatrix);
    }

    public static Rectangle Slice(this Rectangle rect, Rectangle subRect)
    {
        var minX = rect.X + subRect.X;
        var minY = rect.Y + subRect.Y;

        var width = subRect.Width;
        var height = subRect.Height;

        if (minX + width >= subRect.Right || minY + height >= subRect.Bottom)
            throw new ArgumentOutOfRangeException(nameof(subRect));

        return new(minX, minY, width, height);
    }

    public static RectangleF Slice(this RectangleF rect, RectangleF subRect)
    {
        var minX = rect.X + subRect.X;
        var minY = rect.Y + subRect.Y;

        var width = subRect.Width;
        var height = subRect.Height;

        if (minX + width >= subRect.Right || minY + height >= subRect.Bottom)
            throw new ArgumentOutOfRangeException(nameof(subRect));

        return new(minX, minY, width, height);
    }


    public static float ScaleRadius(this AppConfig config, float value)
    {
        return value * config.ClientScale;
    }

    public static void CaptureScreenArea(Bitmap bitmap, Rectangle rect)
    {
        if (bitmap.Size != rect.Size)
            throw new ArgumentException(nameof(bitmap));

        using var captureGraphics = Graphics.FromImage(bitmap);
        captureGraphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size);
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
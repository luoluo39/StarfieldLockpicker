using System.Drawing.Imaging;
using System.Numerics;

namespace StarfieldLockpicker;

public static class Utility
{
    private const int ScreenWidth = 3440;
    private const int ScreenHeight = 1440;

    private const int ReferenceResolutionWidth = 1920;
    private const int ReferenceResolutionHeight = 1080;

    public static Vector2 ScalePosition(Vector2 value)
    {
        return new(ScaleFloatPositionX(value.X), ScaleFloatPositionY(value.Y));
    }

    public static float ScaleRadius(float value)
    {
        return value * ScreenHeight / ReferenceResolutionHeight;
    }

    public static float ScaleFloatPositionX(float value)
    {
        return value * ScreenHeight / ReferenceResolutionHeight +
               (ScreenWidth - (float)ReferenceResolutionWidth *
                   ScreenHeight / ReferenceResolutionHeight) / 2;
    }

    public static float ScaleFloatPositionY(float value)
    {
        return value * ScreenHeight / ReferenceResolutionHeight;
    }

    public static Bitmap FillKeyArea(Bitmap bmp1, Color color)
    {
        Bitmap copied = new(bmp1);

        int width = bmp1.Width;
        int height = bmp1.Height;
        double mse = 0;

        Console.WriteLine(copied.PixelFormat);
        BitmapData data2 = copied.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        //1333,130,494,744 on 1080p

        var x0 = (int)ScaleFloatPositionX(1333);
        var y0 = (int)ScaleFloatPositionY(130);

        var x1 = (int)ScaleFloatPositionX(1333 + 494);
        var y1 = (int)ScaleFloatPositionY(130 + 744);

        unsafe
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var ptr2 = (byte*)data2.Scan0 + (data2.Stride * y + x * 4);
                    for (int i = 0; i < 3; i++) // 4 bytes per pixel (ARGB)
                    {
                        ptr2[i] = 255;
                    }
                }
            }
        }

        copied.UnlockBits(data2);

        return copied;
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

        var x0 = (int)ScaleFloatPositionX(1333);
        var y0 = (int)ScaleFloatPositionY(130);

        var x1 = (int)ScaleFloatPositionX(1333 + 494);
        var y1 = (int)ScaleFloatPositionY(130 + 744);

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

}
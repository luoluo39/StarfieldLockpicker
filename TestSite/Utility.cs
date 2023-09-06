using System.Drawing.Imaging;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace StarfieldLockpicker;

public static class Utility
{
    private const int ScreenWidth = 3440;
    private const int ScreenHeight = 1440;

    private const int ReferenceResolutionWidth = 1920;
    private const int ReferenceResolutionHeight = 1080;

    public static Vector2 TranslatePosition(Vector2 posInReference)
    {
        var config = AppConfig.Instance;

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


    public static Bitmap FillKeyArea(Bitmap bmp1, Color color)
    {
        Bitmap copied = new(bmp1);

        int width = bmp1.Width;
        int height = bmp1.Height;
        double mse = 0;

        Console.WriteLine(copied.PixelFormat);
        //1333,130,494,744 on 1080p

        var x0 = (int)TranslatePositionX(1333);
        var y0 = (int)TranslatePositionY(130);

        var x1 = (int)TranslatePositionX(1333 + 494);
        var y1 = (int)TranslatePositionY(130 + 744);
        Console.WriteLine(copied.GetPixel(x0, y0));

        BitmapData data2 = copied.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        unsafe
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var ptr2 = (byte*)data2.Scan0 + (data2.Stride * y + x * 4);

                    if (x == x0 && y == y0)
                    {
                        var color2 = Color.FromArgb(*(int*)(data2.Scan0 + (data2.Stride * y + x * 4)));
                        Console.WriteLine(color2);

                        Console.WriteLine(ptr2[2]);
                        Console.WriteLine(ptr2[1]);
                        Console.WriteLine(ptr2[0]);
                    }

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

        var x0 = (int)TranslatePositionX(1333);
        var y0 = (int)TranslatePositionY(130);

        var x1 = (int)TranslatePositionX(1333 + 494);
        var y1 = (int)TranslatePositionY(130 + 744);

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
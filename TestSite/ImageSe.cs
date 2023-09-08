using System.Numerics;
using TestSite;

public static class ImageSe
{
    public static uint GradGetLockShape32(Bitmap image, int layer)
    {
        var config = AppConfig.Instance;
        var sp0 = (config.CircleRadius0 + config.CircleRadiusKey) / 2f;
        var sp1 = (config.CircleRadius1 + config.CircleRadius0) / 2f;
        var sp2 = (config.CircleRadius2 + config.CircleRadius1) / 2f;
        var sp3 = (config.CircleRadius3 + config.CircleRadius2) / 2f;
        var sp4 = 2 * sp3 - sp2;

        return layer switch
        {
            0 => GradGetShape32(image, sp1, sp0, config.SampleRadius0, 10f, config.SampleThr0, config.PrintMaxColor0),
            1 => GradGetShape32(image, sp2, sp1, config.SampleRadius1, 10f, config.SampleThr1, config.PrintMaxColor1),
            2 => GradGetShape32(image, sp3, sp2, config.SampleRadius2, 10f, config.SampleThr2, config.PrintMaxColor2),
            3 => GradGetShape32(image, sp4, sp3, config.SampleRadius3, 10f, config.SampleThr3, config.PrintMaxColor3),
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
    }

    private static uint GradGetShape32(Bitmap image, float minRadius, float maxRadius, float sampleRadius, float stepLen, float thr, bool print = false)
    {
        var config = AppConfig.Instance;
        var center = new Vector2(config.CircleCenterX, config.CircleCenterY);
        var scaledCenter = Utility.TranslatePosition(center);
        var scaledMaxRadius = Utility.ScaleRadius(maxRadius);
        var scaledMinRadius = Utility.ScaleRadius(minRadius);
        var scaledStepLen = Utility.ScaleRadius(stepLen);
        var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

        uint v = 0;
        for (var i = 0; i < 32; i++)
        {
            var x = 2 * float.Pi * i / 32;
            var (sin, cos) = float.SinCos(x);

            var last = float.NaN;
            for (var s = scaledMinRadius; s < scaledMaxRadius; s += scaledStepLen)
            {
                var pos = new Vector2(cos, sin) * s + scaledCenter;
                var current = Utility.CalculateMaxB(image, pos, scaledSampleRadius);

                if (!float.IsNaN(last) && print)
                {
                    Console.WriteLine(current - last);
                }
                if (!float.IsNaN(last) && current - last >= thr)
                {
                    v |= 1U << i;
                    break;
                }
                last = current;
            }
        }

        return v;
    }

    public static uint GetKeyShape32(Bitmap image)
    {
        var config = AppConfig.Instance;
        return GetShape32(image, config.CircleRadiusKey, config.SampleRadiusKey, config.SampleThrKey, config.PrintMaxColorKey);
    }

    private static uint GetShape32(Bitmap image, float circleRadius, float sampleRadius, float thr, bool print = false)
    {
        var config = AppConfig.Instance;
        var center = new Vector2(config.CircleCenterX, config.CircleCenterY);
        var scaledCenter = Utility.TranslatePosition(center);
        var scaledRadius = Utility.ScaleRadius(circleRadius);
        var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

        uint v = 0;
        for (var i = 0; i < 32; i++)
        {
            var x = 2 * float.Pi * i / 32;
            var (sin, cos) = float.SinCos(x);
            var pos = new Vector2(cos, sin) * scaledRadius + scaledCenter;
            var gray = Utility.CalculateMaxB(image, pos, scaledSampleRadius);

            if (print) Console.Write($",{gray:F2}");

            v |= gray > thr ? 1U << i : 0;
        }
        if (print) Console.WriteLine();

        return v;
    }
}
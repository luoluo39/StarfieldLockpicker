using System.Numerics;
using StarfieldLockpicker.Core;

namespace StarfieldLockpicker;

public class WrappedBitmap : IFullImage
{
    private readonly AppConfig _config;
    private bool _disposed;

    public Bitmap Bitmap { get; }
    public Rectangle ScreenSpaceBounds { get; }

    public WrappedBitmap(Bitmap bitmap, AppConfig config, Rectangle screenSpaceBounds)
    {
        Bitmap = bitmap;
        _config = config;
        ScreenSpaceBounds = screenSpaceBounds;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Bitmap.Dispose();
            _disposed = true;
        }
    }

    public double KeyAreaMseWith(IKeySelectionImage other)
    {
        var rect = Utility.TranslateRectangleCeiling(
            new Rectangle(
                _config.KeyAreaX0, 
                _config.KeyAreaY0, 
                _config.KeyAreaWidth, 
                _config.KeyAreaHeight)
            );

        return other switch
        {
            WrappedBitmap bitmap => Utility.CalculateMSE(this, bitmap, rect),
            PooledWrappedBitmap bitmap2 => Utility.CalculateMSE(this, bitmap2.Inner, rect),
            _ => throw new NotSupportedException()
        };
    }

    public uint GetLockShape(int layer)
    {
        return GradGetLockShape32(layer);
    }

    public uint GetKeyShape()
    {
        return GetKeyShape32();
    }

    private Vector2 TranslateScreenPosition(Vector2 screenSpacePos)
    {
        return screenSpacePos - new Vector2(ScreenSpaceBounds.X, ScreenSpaceBounds.Y);
    }

    private uint GetKeyShape32()
    {
        return GetShape32(_config.CircleRadiusKey, _config.SampleRadiusKey, _config.SampleThrKey, _config.PrintMaxColorKey);
    }

    private uint GetShape32(float circleRadius, float sampleRadius, float thr, bool print = false)
    {
        var center = new Vector2(_config.CircleCenterX, _config.CircleCenterY);

        var scaledCenter = TranslateScreenPosition(Utility.TranslatePosition(center));
        var scaledRadius = Utility.ScaleRadius(circleRadius);
        var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

        uint v = 0;
        for (var i = 0; i < 32; i++)
        {
            var x = 2 * float.Pi * i / 32;
            var (sin, cos) = float.SinCos(x);
            var pos = new Vector2(cos, sin) * scaledRadius + scaledCenter;
            var gray = Utility.CalculateMaxB(Bitmap, pos, scaledSampleRadius);

            if (print) Console.Write($",{gray:F2}");

            v |= gray > thr ? 1U << i : 0;
        }
        if (print) Console.WriteLine();

        return v;
    }

    private uint GradGetLockShape32(int layer)
    {
        var sp0 = (_config.CircleRadius0 + _config.CircleRadiusKey) / 2f;
        var sp1 = (_config.CircleRadius1 + _config.CircleRadius0) / 2f;
        var sp2 = (_config.CircleRadius2 + _config.CircleRadius1) / 2f;
        var sp3 = (_config.CircleRadius3 + _config.CircleRadius2) / 2f;
        var sp4 = 2 * sp3 - sp2;

        return layer switch
        {
            0 => GradGetShape32(sp1, sp0, _config.SampleRadius0, 10f, _config.SampleThr0, _config.PrintMaxColor0),
            1 => GradGetShape32(sp2, sp1, _config.SampleRadius1, 10f, _config.SampleThr1, _config.PrintMaxColor1),
            2 => GradGetShape32(sp3, sp2, _config.SampleRadius2, 10f, _config.SampleThr2, _config.PrintMaxColor2),
            3 => GradGetShape32(sp4, sp3, _config.SampleRadius3, 10f, _config.SampleThr3, _config.PrintMaxColor3),
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
    }

    private uint GradGetShape32(float minRadius, float maxRadius, float sampleRadius, float stepLen, float thr, bool print = false)
    {
        var center = new Vector2(_config.CircleCenterX, _config.CircleCenterY);
        var scaledCenter = TranslateScreenPosition(Utility.TranslatePosition(center));
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
                var current = Utility.CalculateMaxB(Bitmap, pos, scaledSampleRadius);

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
}
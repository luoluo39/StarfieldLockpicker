// See https://aka.ms/new-console-template for more information

using System.Drawing.Imaging;
using System.Numerics;
using StarfieldLockpicker;

Console.WriteLine("Hello, World!");

uint[] lockShapes = new uint[4];

var bm = (Bitmap)Image.FromFile(@"C:\Users\24580\Downloads\15ROnfk.jpeg");

var bm1 = Utility.FillKeyArea(bm, Color.AliceBlue);
bm1.Save("awgahrah.png", ImageFormat.Png);
var shape0 = GetShape32(bm,
    205,
    10,
    0.175f,
    true);

var shape1 = GetShape32(bm,
    168,
    10,
    0.175f,
    true);

var shape2 = GetShape32(bm,
    135,
    10,
    0.175f,
    true);

var shape3 = GetShape32(bm,
    105,
    7,
    0.175f,
    true);

var shapeK = GetShape32(bm,
    240,
    7,
    0.175f,
    true);

var shapex0 = GradGetLockShape32(bm, 0);
var shapex1 = GradGetLockShape32(bm, 1);
var shapex2 = GradGetLockShape32(bm, 2);
var shapex3 = GradGetLockShape32(bm, 3);

ConsoleWriteShape32(shapeK);

Console.WriteLine();
ConsoleWriteShape32(shape0);
ConsoleWriteShape32(shapex0);
Console.WriteLine();
ConsoleWriteShape32(shape1);
ConsoleWriteShape32(shapex1);
Console.WriteLine();
ConsoleWriteShape32(shape2);
ConsoleWriteShape32(shapex2);
Console.WriteLine();
ConsoleWriteShape32(shape3);
ConsoleWriteShape32(shapex3);
Console.WriteLine();
static void ConsoleWriteShape32(uint shape)
{
    for (int i = 0; i < 32; i++)
    {
        var x = (shape >> i) & 1;
        Console.Write(x);
    }
    Console.WriteLine();
}

uint GetShape32(Bitmap image, float circleRadius, float sampleRadius, float thr, bool print = false)
{
    var center = new Vector2(960, 540);
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

uint GradGetLockShape32(Bitmap image, int layer)
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

uint GradGetShape32(Bitmap image, float minRadius, float maxRadius, float sampleRadius, float stepLen, float thr, bool print = false)
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

public class Bitmap2
{
    private Vector3[] buffer;
    public int Width { get; }
    public int Height { get; }

    public Vector3 this[int x, int y] => buffer[y * Width + Height];

    public Vector3 this[float x, float y]
    {
        get
        {
            int x0 = (int)x;
            int x1 = x0 + 1;
            int y0 = (int)y;
            int y1 = y0 + 1;

            float tx = x - x0;
            float ty = y - y0;

            // 边界检查
            if (x0 < 0) x0 = 0;
            if (x1 >= Width) x1 = Width - 1;
            if (y0 < 0) y0 = 0;
            if (y1 >= Height) y1 = Height - 1;

            // 四个角的颜色
            Vector3 c00 = buffer[y0 * Width + x0];
            Vector3 c01 = buffer[y0 * Width + x1];
            Vector3 c10 = buffer[y1 * Width + x0];
            Vector3 c11 = buffer[y1 * Width + x1];

            // 在 x 方向进行线性插值
            Vector3 top = Vector3.Lerp(c00, c01, tx);
            Vector3 bottom = Vector3.Lerp(c10, c11, tx);

            // 在 y 方向进行线性插值
            return Vector3.Lerp(top, bottom, ty);
        }
    }
}